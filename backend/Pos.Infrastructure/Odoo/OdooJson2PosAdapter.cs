using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Application.DTOs;
using Pos.Application.Exceptions;
using Pos.Application.Ports;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Odoo;

/// <summary>
/// Caché en memoria thread-safe compartida (Singleton en DI) para evitar consultas repetitivas a Odoo 20
/// en conteos paginados (TTL 30s), impuestos (TTL 5m) y partners excluidos del POS (TTL 5m).
/// </summary>
public sealed class OdooAdapterCache
{
    private static readonly TimeSpan CountCacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TaxCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ExcludedPartnersCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(string Type, string NormalizedQuery, string Criteria, bool OnlyInStock), (int TotalCount, DateTime ExpiresAtUtc)> _countCache = new();
    private readonly ConcurrentDictionary<int, (decimal Rate, bool PriceInclude, bool IsPercent, DateTime ExpiresAtUtc)> _taxCache = new();

    private IReadOnlyList<int>? _excludedPartnerIds;
    private DateTime _excludedPartnerIdsExpiresAtUtc = DateTime.MinValue;

    public SemaphoreSlim ExcludedPartnersLock { get; } = new(1, 1);

    public bool TryGetCount(string type, string normalizedQuery, string criteria, bool onlyInStock, out int totalCount)
    {
        var key = (type, normalizedQuery, criteria, onlyInStock);
        if (_countCache.TryGetValue(key, out var entry) && entry.ExpiresAtUtc > DateTime.UtcNow)
        {
            totalCount = entry.TotalCount;
            return true;
        }
        totalCount = 0;
        return false;
    }

    public void SetCount(string type, string normalizedQuery, string criteria, bool onlyInStock, int totalCount)
    {
        var now = DateTime.UtcNow;
        if (_countCache.Count > 1000)
        {
            foreach (var kv in _countCache)
            {
                if (kv.Value.ExpiresAtUtc <= now)
                {
                    _countCache.TryRemove(kv.Key, out _);
                }
            }
        }
        _countCache[(type, normalizedQuery, criteria, onlyInStock)] = (totalCount, now.Add(CountCacheTtl));
    }

    public bool TryGetTax(int taxId, out (decimal Rate, bool PriceInclude, bool IsPercent) entry)
    {
        if (_taxCache.TryGetValue(taxId, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
        {
            entry = (cached.Rate, cached.PriceInclude, cached.IsPercent);
            return true;
        }
        entry = default;
        return false;
    }

    public void SetTax(int taxId, decimal rate, bool priceInclude, bool isPercent)
    {
        _taxCache[taxId] = (rate, priceInclude, isPercent, DateTime.UtcNow.Add(TaxCacheTtl));
    }

    public bool TryGetExcludedPartnerIds(out IReadOnlyList<int> ids)
    {
        var current = Volatile.Read(ref _excludedPartnerIds);
        if (current is not null && DateTime.UtcNow < _excludedPartnerIdsExpiresAtUtc)
        {
            ids = current;
            return true;
        }
        ids = Array.Empty<int>();
        return false;
    }

    public void SetExcludedPartnerIds(IReadOnlyList<int> ids)
    {
        _excludedPartnerIdsExpiresAtUtc = DateTime.UtcNow.Add(ExcludedPartnersCacheTtl);
        Volatile.Write(ref _excludedPartnerIds, ids);
    }
}

/// <summary>
/// Adapter (GoF Estructural): traduce el puerto <see cref="IOdooPosGateway"/> de la capa Application
/// a la API JSON-2 de Odoo 20 (<c>POST /json/2/{model}/{method}</c> con autenticación Bearer por API Key).
/// </summary>
public class OdooJson2PosAdapter : IOdooPosGateway
{
    private const int MaxCountLimit = 10000;
    private const int AllBranchProbeLimit = 5001;
    private const int MaxIndexedUnionCardinality = 5000;

    private static readonly string[] ProductFields =
        { "id", "name", "default_code", "lst_price", "barcode", "qty_available", "taxes_id" };

    private static readonly string[] CustomerFields =
        { "id", "name", "vat", "email", "street", "phone" };

    private static readonly Regex LeadingIdPrefixRegex =
        new(@"^(?:#\s*|id\s*:\s*)+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly OdooOptions _options;
    private readonly ILogger<OdooJson2PosAdapter> _logger;
    private readonly OdooAdapterCache _cache;

    public OdooJson2PosAdapter(
        HttpClient httpClient,
        IOptions<OdooOptions> options,
        ILogger<OdooJson2PosAdapter> logger)
        : this(httpClient, options, logger, new OdooAdapterCache())
    {
    }

    [ActivatorUtilitiesConstructor]
    public OdooJson2PosAdapter(
        HttpClient httpClient,
        IOptions<OdooOptions> options,
        ILogger<OdooJson2PosAdapter> logger,
        OdooAdapterCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _cache = cache;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }
    }

    // ------------------------------------------------------------------
    // Catálogo (RF-01, RF-02, RN-03)
    // ------------------------------------------------------------------

    private static string ExtractCleanNumericId(string query) =>
        LeadingIdPrefixRegex.Replace(query.Trim(), string.Empty).Trim();

    private static string EscapeOdooLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static bool IsNumericPrefix(string input) =>
        input.Length > 0 && input.All(char.IsDigit);

    private static List<object> BuildBaseProductDomain(bool onlyInStock = false)
    {
        var domain = new List<object>
        {
            new object[] { "available_in_pos", "=", true },
            new object[] { "sale_ok", "=", true }
        };

        if (onlyInStock)
        {
            domain.Add(new object[] { "qty_available", ">", 0 });
        }

        return domain;
    }

    private static List<object> BuildProductDomain(string query, string? criteria = null, bool onlyInStock = false)
    {
        var domain = BuildBaseProductDomain(onlyInStock);

        if (string.IsNullOrWhiteSpace(query))
        {
            return domain;
        }

        var q = query.Trim();
        var cleanNum = ExtractCleanNumericId(q);
        var crit = criteria?.Trim().ToUpperInvariant();

        if (crit == "ID")
        {
            if (int.TryParse(cleanNum, out var pid))
            {
                domain.Add(new object[] { "id", "=", pid });
            }
            else
            {
                domain.Add(new object[] { "id", "=", -1 });
            }
        }
        else if (crit == "NAME")
        {
            domain.Add(new object[] { "name", "ilike", q });
        }
        else if (crit == "CODE")
        {
            domain.Add("|");
            domain.Add(new object[] { "default_code", "ilike", q });
            domain.Add(new object[] { "barcode", "ilike", q });
        }
        else // "ALL" o nulo (búsqueda general)
        {
            if (int.TryParse(cleanNum, out var parsedId))
            {
                domain.Add("|");
                domain.Add("|");
                domain.Add("|");
                domain.Add(new object[] { "id", "=", parsedId });
                domain.Add(new object[] { "name", "ilike", q });
                domain.Add(new object[] { "default_code", "ilike", q });
                domain.Add(new object[] { "barcode", "ilike", q });
            }
            else
            {
                domain.Add("|");
                domain.Add("|");
                domain.Add(new object[] { "name", "ilike", q });
                domain.Add(new object[] { "default_code", "ilike", q });
                domain.Add(new object[] { "barcode", "ilike", q });
            }
        }

        return domain;
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        var domain = BuildProductDomain(query);
        return await ReadProductsAsync(domain, limit: 100, offset: 0, ct: cancellationToken);
    }

    public async Task<(IReadOnlyList<ProductDto> Items, int TotalCount)> SearchProductsPagedAsync(
        string query,
        int page = 1,
        int pageSize = 8,
        string? criteria = null,
        bool onlyInStock = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var normalizedQuery = query?.Trim().ToUpperInvariant() ?? string.Empty;
        var crit = criteria?.Trim().ToUpperInvariant();
        var normalizedCrit = string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(crit) ? "ALL" : crit;
        var isGeneralSearch = !string.IsNullOrWhiteSpace(query) && normalizedCrit == "ALL";

        if (!isGeneralSearch)
        {
            var domain = BuildProductDomain(query ?? string.Empty, criteria, onlyInStock);
            int? countLimit = string.IsNullOrWhiteSpace(query) ? null : MaxCountLimit;
            return await ReadPagedWithShortCircuitAndCacheAsync(
                cacheType: "product",
                normalizedQuery: normalizedQuery,
                normalizedCriteria: normalizedCrit,
                onlyInStock: onlyInStock,
                page: page,
                pageSize: pageSize,
                readPageAsync: () => ReadProductsWithRawCountAsync(domain, limit: pageSize, offset: offset, ct: cancellationToken),
                countAsync: () => CountModelAsync("product.product", domain, countLimit, cancellationToken));
        }

        return await SearchProductsAllIndexedAsync(query!.Trim(), normalizedQuery, pageSize, offset, onlyInStock, cancellationToken);
    }

    private async Task<(IReadOnlyList<T> Items, int TotalCount)> ReadPagedWithShortCircuitAndCacheAsync<T>(
        string cacheType,
        string normalizedQuery,
        string normalizedCriteria,
        bool onlyInStock,
        int page,
        int pageSize,
        Func<Task<(IReadOnlyList<T> Items, int RawCount)>> readPageAsync,
        Func<Task<int>> countAsync)
    {
        if (_cache.TryGetCount(cacheType, normalizedQuery, normalizedCriteria, onlyInStock, out var cachedTotal))
        {
            var (cachedItems, _) = await readPageAsync();
            return (cachedItems, cachedTotal);
        }

        if (page == 1)
        {
            var (items, rawCount) = await readPageAsync();
            if (rawCount < pageSize)
            {
                _cache.SetCount(cacheType, normalizedQuery, normalizedCriteria, onlyInStock, items.Count);
                return (items, items.Count);
            }

            var totalCount = await countAsync();
            _cache.SetCount(cacheType, normalizedQuery, normalizedCriteria, onlyInStock, totalCount);
            return (items, totalCount);
        }

        var countTask = countAsync();
        var readTask = readPageAsync();
        await Task.WhenAll(countTask, readTask);

        var total = countTask.Result;
        var (pagedItems, _) = readTask.Result;
        _cache.SetCount(cacheType, normalizedQuery, normalizedCriteria, onlyInStock, total);
        return (pagedItems, total);
    }

    private async Task<(IReadOnlyList<ProductDto> Items, int TotalCount)> SearchProductsAllIndexedAsync(
        string q,
        string normalizedQuery,
        int pageSize,
        int offset,
        bool onlyInStock,
        CancellationToken ct)
    {
        var baseDomain = BuildBaseProductDomain(onlyInStock);
        var nameDomain = new List<object>(baseDomain)
        {
            new object[] { "name", "ilike", q }
        };
        var codeDomain = new List<object>(baseDomain)
        {
            "|",
            new object[] { "default_code", "ilike", q },
            new object[] { "barcode", "ilike", q }
        };

        var cleanNum = ExtractCleanNumericId(q);
        var hasParsedId = int.TryParse(cleanNum, out var parsedId);
        List<object>? idDomain = hasParsedId
            ? new List<object>(baseDomain) { new object[] { "id", "=", parsedId } }
            : null;

        var nameCountTask = CountModelAsync("product.product", nameDomain, AllBranchProbeLimit, ct);
        var codeCountTask = CountModelAsync("product.product", codeDomain, AllBranchProbeLimit, ct);
        var idCountTask = idDomain is not null ? CountModelAsync("product.product", idDomain, 1, ct) : Task.FromResult(0);

        await Task.WhenAll(nameCountTask, codeCountTask, idCountTask);

        var nameCount = nameCountTask.Result;
        var codeCount = codeCountTask.Result;
        var idCount = idCountTask.Result;
        var idExists = idCount > 0;

        if (nameCount == 0 && codeCount == 0 && !idExists)
        {
            _cache.SetCount("product", normalizedQuery, "ALL", onlyInStock, 0);
            return (Array.Empty<ProductDto>(), 0);
        }

        if (nameCount <= MaxIndexedUnionCardinality && codeCount <= MaxIndexedUnionCardinality)
        {
            // Si solo una rama tiene coincidencias, leemos directamente esa rama paginada para aprovechar índices GIN/PK.
            if (nameCount > 0 && codeCount == 0 && !idExists)
            {
                var items = await ReadProductsAsync(nameDomain, limit: pageSize, offset: offset, ct: ct);
                _cache.SetCount("product", normalizedQuery, "ALL", onlyInStock, nameCount);
                return (items, nameCount);
            }

            if (codeCount > 0 && nameCount == 0 && !idExists)
            {
                var items = await ReadProductsAsync(codeDomain, limit: pageSize, offset: offset, ct: ct);
                _cache.SetCount("product", normalizedQuery, "ALL", onlyInStock, codeCount);
                return (items, codeCount);
            }

            if (idExists && nameCount == 0 && codeCount == 0)
            {
                var items = await ReadProductsAsync(idDomain!, limit: pageSize, offset: offset, ct: ct);
                _cache.SetCount("product", normalizedQuery, "ALL", onlyInStock, idCount);
                return (items, idCount);
            }

            // Múltiples ramas con coincidencias: unión de IDs sin duplicados cuando la cardinalidad es acotada.
            var idSearchTasks = new List<Task<IReadOnlyList<int>>>(3);
            if (nameCount > 0) idSearchTasks.Add(SearchProductIdsAsync(nameDomain, ct));
            if (codeCount > 0) idSearchTasks.Add(SearchProductIdsAsync(codeDomain, ct));
            if (idExists && idDomain is not null) idSearchTasks.Add(SearchProductIdsAsync(idDomain, ct));

            var idLists = await Task.WhenAll(idSearchTasks);
            var distinctIds = idLists
                .SelectMany(ids => ids)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            var totalCount = distinctIds.Count;
            _cache.SetCount("product", normalizedQuery, "ALL", onlyInStock, totalCount);

            var pageIds = distinctIds.Skip(offset).Take(pageSize).ToArray();
            if (pageIds.Length == 0)
            {
                return (Array.Empty<ProductDto>(), totalCount);
            }

            var pageDomain = new List<object>(baseDomain)
            {
                new object[] { "id", "in", pageIds }
            };
            var unionItems = await ReadProductsAsync(pageDomain, limit: pageSize, offset: 0, ct: ct);
            return (unionItems.OrderBy(p => p.Id).ToList(), totalCount);
        }

        // Fallback para alta cardinalidad (>= 5001 en alguna rama): evita traer miles de IDs, usa order id asc y conteo acotado a 10000.
        var fallbackDomain = BuildProductDomain(q, "ALL", onlyInStock);
        if (_cache.TryGetCount("product", normalizedQuery, "ALL", onlyInStock, out var cachedFallbackCount))
        {
            var cachedFallbackItems = await ReadProductsAsync(fallbackDomain, limit: pageSize, offset: offset, ct: ct);
            return (cachedFallbackItems, cachedFallbackCount);
        }

        var fallbackCountTask = CountModelAsync("product.product", fallbackDomain, MaxCountLimit, ct);
        var fallbackItemsTask = ReadProductsAsync(fallbackDomain, limit: pageSize, offset: offset, ct: ct);
        await Task.WhenAll(fallbackCountTask, fallbackItemsTask);

        var fallbackCount = fallbackCountTask.Result;
        _cache.SetCount("product", normalizedQuery, "ALL", onlyInStock, fallbackCount);
        return (fallbackItemsTask.Result, fallbackCount);
    }

    private async Task<int> CountModelAsync(string model, List<object> domain, int? limit, CancellationToken ct)
    {
        object payload = limit.HasValue
            ? new { domain, limit = limit.Value }
            : new { domain };
        var countNode = await CallAsync(model, "search_count", payload, ct);
        return countNode is JsonValue v && v.TryGetValue<int>(out var c)
            ? c
            : (int.TryParse(countNode?.ToString(), out var parsedC) ? parsedC : 0);
    }

    private async Task<IReadOnlyList<int>> SearchProductIdsAsync(List<object> domain, CancellationToken ct)
    {
        var result = await CallAsync("product.product", "search", new { domain, order = "id asc" }, ct);
        return ReadIdList(result).ToList();
    }

    private async Task<IReadOnlyList<int>> GetExcludedPartnerIdsAsync(CancellationToken ct)
    {
        if (_cache.TryGetExcludedPartnerIds(out var cached))
        {
            return cached;
        }

        await _cache.ExcludedPartnersLock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetExcludedPartnerIds(out cached))
            {
                return cached;
            }

            var usersTask = FetchIdsSafeAsync(
                "res.users",
                "search_read",
                new
                {
                    domain = new object[] { new object[] { "partner_id", "!=", false } },
                    fields = new[] { "partner_id" },
                    context = new { active_test = false }
                },
                node => ExtractMany2OnePartnerIds(node),
                ct);

            var companiesTask = FetchIdsSafeAsync(
                "res.company",
                "search_read",
                new
                {
                    domain = Array.Empty<object>(),
                    fields = new[] { "partner_id" }
                },
                node => ExtractMany2OnePartnerIds(node),
                ct);

            var finalConsumerTask = FetchIdsSafeAsync(
                "res.partner",
                "search",
                new
                {
                    domain = new object[]
                    {
                        "|",
                        new object[] { "vat", "=", "9999999999999" },
                        new object[] { "name", "ilike", "Consumidor Final" }
                    }
                },
                node => ReadIdList(node),
                ct);

            await Task.WhenAll(usersTask, companiesTask, finalConsumerTask);

            var merged = usersTask.Result.Ids
                .Concat(companiesTask.Result.Ids)
                .Concat(finalConsumerTask.Result.Ids)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            if (usersTask.Result.Succeeded && companiesTask.Result.Succeeded && finalConsumerTask.Result.Succeeded)
            {
                _cache.SetExcludedPartnerIds(merged);
            }

            return merged;
        }
        finally
        {
            _cache.ExcludedPartnersLock.Release();
        }
    }

    private async Task<(IReadOnlyList<int> Ids, bool Succeeded)> FetchIdsSafeAsync(
        string model,
        string method,
        object body,
        Func<JsonNode?, IEnumerable<int>> extractor,
        CancellationToken ct)
    {
        try
        {
            var result = await CallAsync(model, method, body, ct);
            return (extractor(result).ToArray(), true);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "No se pudo consultar IDs excluidos en {Model}.{Method}; se omite esta fuente.", model, method);
            return (Array.Empty<int>(), false);
        }
    }

    private static IEnumerable<int> ExtractMany2OnePartnerIds(JsonNode? node)
    {
        if (node is not JsonArray rows) yield break;
        foreach (var row in rows)
        {
            if (row is null) continue;
            var (partnerId, _) = ReadMany2One(row["partner_id"]);
            if (partnerId > 0) yield return partnerId;
        }
    }

    private static List<object> BuildCustomerDomain(string query, string? criteria = null, IReadOnlyList<int>? excludedPartnerIds = null)
    {
        // [FILTRADO ESTRICTO DE CLIENTES EXTERNOS EN ODOO]:
        // En lugar de filtros negativos costosos (user_ids = false, ref_company_ids = false, not ilike),
        // excluimos los IDs cacheados de usuarios internos, compañías y Consumidor Final mediante id not in.
        var domain = new List<object>
        {
            new object[] { "active", "=", true }
        };

        if (excludedPartnerIds is { Count: > 0 })
        {
            domain.Add(new object[] { "id", "not in", excludedPartnerIds });
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return domain;
        }

        var q = query.Trim();
        var cleanNum = ExtractCleanNumericId(q);
        var crit = criteria?.Trim().ToUpperInvariant();

        if (crit == "ID")
        {
            if (int.TryParse(cleanNum, out var cid))
            {
                domain.Add(new object[] { "id", "=", cid });
            }
            else
            {
                domain.Add(new object[] { "id", "=", -1 });
            }
        }
        else if (crit == "NAME")
        {
            domain.Add(new object[] { "name", "ilike", q });
        }
        else if (crit == "VAT")
        {
            var escapedUpper = EscapeOdooLike(q).ToUpperInvariant();
            domain.Add(new object[] { "vat", "=like", $"{escapedUpper}%" });
        }
        else // "ALL" o nulo (búsqueda general)
        {
            object vatCondition = (IsNumericPrefix(q) || q.Length < 3)
                ? new object[] { "vat", "=like", $"{EscapeOdooLike(q).ToUpperInvariant()}%" }
                : new object[] { "vat", "ilike", q };

            if (int.TryParse(cleanNum, out var parsedId))
            {
                domain.Add("|");
                domain.Add("|");
                domain.Add("|");
                domain.Add(new object[] { "id", "=", parsedId });
                domain.Add(new object[] { "name", "ilike", q });
                domain.Add(vatCondition);
                domain.Add(new object[] { "email", "ilike", q });
            }
            else
            {
                domain.Add("|");
                domain.Add("|");
                domain.Add(new object[] { "name", "ilike", q });
                domain.Add(vatCondition);
                domain.Add(new object[] { "email", "ilike", q });
            }
        }

        return domain;
    }

    private static List<CustomerDto> ParseCustomers(JsonNode? result)
    {
        var rows = result?.AsArray() ?? new JsonArray();
        var customers = new List<CustomerDto>(rows.Count);
        foreach (var row in rows)
        {
            if (row is null) continue;
            var id = row["id"]!.GetValue<int>();
            var name = ReadString(row["name"]) is { Length: > 0 } n ? n : $"Cliente {id}";
            var vat = ReadString(row["vat"]);
            var email = ReadString(row["email"]);
            var street = ReadString(row["street"]);
            var phone = ReadString(row["phone"]);

            // Filtro defensivo adicional: omitir Consumidor Final o identificaciones SRI genéricas
            if (string.Equals(vat, "9999999999999", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Consumidor Final", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            customers.Add(new CustomerDto(id, name, vat, email, street, phone));
        }
        return customers;
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(string query, CancellationToken cancellationToken = default)
    {
        var (items, _) = await SearchCustomersPagedAsync(query, page: 1, pageSize: 50, criteria: null, cancellationToken: cancellationToken);
        return items;
    }

    public async Task<(IReadOnlyList<CustomerDto> Items, int TotalCount)> SearchCustomersPagedAsync(
        string query,
        int page = 1,
        int pageSize = 8,
        string? criteria = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var normalizedQuery = query?.Trim().ToUpperInvariant() ?? string.Empty;
        var crit = criteria?.Trim().ToUpperInvariant();
        var normalizedCrit = !hasQuery || string.IsNullOrEmpty(crit) ? "ALL" : crit;

        var excludedIds = await GetExcludedPartnerIdsAsync(cancellationToken);
        var domain = BuildCustomerDomain(query ?? string.Empty, criteria, excludedIds);
        int? countLimit = hasQuery ? MaxCountLimit : null;

        return await ReadPagedWithShortCircuitAndCacheAsync(
            cacheType: "customer",
            normalizedQuery: normalizedQuery,
            normalizedCriteria: normalizedCrit,
            onlyInStock: false,
            page: page,
            pageSize: pageSize,
            readPageAsync: () => ReadCustomersWithRawCountAsync(domain, pageSize, offset, hasQuery, cancellationToken),
            countAsync: () => CountModelAsync("res.partner", domain, countLimit, cancellationToken));
    }

    private async Task<(IReadOnlyList<CustomerDto> Items, int RawCount)> ReadCustomersWithRawCountAsync(
        List<object> domain,
        int limit,
        int offset,
        bool hasQuery,
        CancellationToken ct)
    {
        object payload = hasQuery
            ? new
            {
                domain,
                fields = CustomerFields,
                limit,
                offset,
                order = "id asc"
            }
            : new
            {
                domain,
                fields = CustomerFields,
                limit,
                offset
            };

        var result = await CallAsync("res.partner", "search_read", payload, ct);
        var rows = result?.AsArray() ?? new JsonArray();
        return (ParseCustomers(result), rows.Count);
    }

    public Task<IReadOnlyList<ProductDto>> GetProductsByIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
        => GetProductsByIdsAsync(productIds.ToArray(), cancellationToken);

    public async Task<IReadOnlyList<ProductDto>> GetProductsByIdsAsync(int[] productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Length == 0) return Array.Empty<ProductDto>();
        var distinctIds = productIds.Distinct().ToArray();
        var domain = new List<object>
        {
            new object[] { "id", "in", distinctIds }
        };
        return await ReadProductsAsync(domain, limit: distinctIds.Length, offset: 0, ct: cancellationToken);
    }

    private async Task<IReadOnlyList<ProductDto>> ReadProductsAsync(List<object> domain, int limit, int offset = 0, CancellationToken ct = default)
    {
        var (items, _) = await ReadProductsWithRawCountAsync(domain, limit, offset, ct);
        return items;
    }

    private async Task<(IReadOnlyList<ProductDto> Items, int RawCount)> ReadProductsWithRawCountAsync(
        List<object> domain,
        int limit,
        int offset = 0,
        CancellationToken ct = default)
    {
        var result = await CallAsync("product.product", "search_read", new
        {
            domain,
            fields = ProductFields,
            limit,
            offset,
            order = "id asc"
        }, ct);

        var rows = result?.AsArray() ?? new JsonArray();

        // Resolver la configuración fiscal (account.tax) de todos los productos en una sola llamada (con caché TTL).
        var allTaxIds = rows
            .SelectMany(r => ReadIdList(r?["taxes_id"]))
            .Distinct()
            .ToArray();
        var taxes = await ReadTaxesAsync(allTaxIds, ct);

        var products = new List<ProductDto>(rows.Count);
        foreach (var row in rows)
        {
            if (row is null) continue;

            var id = row["id"]!.GetValue<int>();
            var taxIds = ReadIdList(row["taxes_id"]).ToList();
            var productTaxes = taxIds.Where(taxes.ContainsKey).Select(t => taxes[t]).ToList();

            var rate = productTaxes.Sum(t => t.Rate);
            var priceIncludesTax = productTaxes.Count > 0 && productTaxes.All(t => t.PriceInclude);

            products.Add(new ProductDto(
                Id: id,
                Name: ReadString(row["name"]) is { Length: > 0 } n ? n : $"Producto {id}",
                Price: ReadDecimal(row["lst_price"]),
                Barcode: ReadString(row["barcode"]),
                AvailableQuantity: ReadDecimal(row["qty_available"]),
                DefaultCode: ReadString(row["default_code"]),
                TaxRate: rate,
                PriceIncludesTax: priceIncludesTax,
                TaxIds: taxIds));
        }

        return (products, rows.Count);
    }

    private async Task<Dictionary<int, (decimal Rate, bool PriceInclude)>> ReadTaxesAsync(int[] taxIds, CancellationToken ct)
    {
        var map = new Dictionary<int, (decimal Rate, bool PriceInclude)>();
        if (taxIds.Length == 0) return map;

        var missingIds = new List<int>(taxIds.Length);
        foreach (var id in taxIds)
        {
            if (_cache.TryGetTax(id, out var cached))
            {
                if (cached.IsPercent)
                {
                    map[id] = (cached.Rate, cached.PriceInclude);
                }
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (missingIds.Count == 0)
        {
            return map;
        }

        try
        {
            var result = await CallAsync("account.tax", "search_read", new
            {
                domain = new object[] { new object[] { "id", "in", missingIds.ToArray() } },
                fields = new[] { "id", "amount", "amount_type", "price_include" }
            }, ct);

            foreach (var tax in result?.AsArray() ?? new JsonArray())
            {
                if (tax is null) continue;
                var taxId = tax["id"]!.GetValue<int>();
                if (!string.Equals(ReadString(tax["amount_type"]), "percent", StringComparison.Ordinal))
                {
                    _logger.LogWarning("Impuesto {TaxId} con amount_type no porcentual: se ignora en el cálculo local.", tax["id"]);
                    _cache.SetTax(taxId, 0m, false, isPercent: false);
                    continue;
                }

                var rate = ReadDecimal(tax["amount"]) / 100m;
                var priceInclude = tax["price_include"]?.GetValue<bool>() == true;
                _cache.SetTax(taxId, rate, priceInclude, isPercent: true);
                map[taxId] = (rate, priceInclude);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "No se pudo consultar account.tax en Odoo para IDs [{TaxIds}]; se continúa con el mapa fiscal disponible.", string.Join(",", missingIds));
        }

        return map;
    }

    // ------------------------------------------------------------------
    // Autenticación de Operadores / Vendedores con Odoo 20 (Fase 4)
    // ------------------------------------------------------------------

    public async Task<OdooUserAuthDto?> AuthenticateUserAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var authPayload = new
        {
            jsonrpc = "2.0",
            @params = new
            {
                db = _options.Database,
                login = username,
                password = password
            }
        };

        var jsonBody = JsonSerializer.Serialize(authPayload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "web/session/authenticate")
        {
            Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OdooCommunicationException($"No fue posible conectar con Odoo para autenticar al usuario '{username}': {ex.Message}", ex);
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Odoo devolvió HTTP {StatusCode} al autenticar a '{Username}': {Raw}", response.StatusCode, username, raw);
                return null;
            }

            JsonNode? rootNode = null;
            try
            {
                rootNode = JsonNode.Parse(raw);
            }
            catch (JsonException)
            {
                return null;
            }

            if (rootNode == null || rootNode["error"] != null)
            {
                _logger.LogWarning("Autenticación fallida en Odoo para '{Username}'. Error: {Error}", username, rootNode?["error"]?.ToJsonString());
                return null;
            }

            var result = rootNode["result"];
            if (result == null)
            {
                return null;
            }

            var uid = result["uid"]?.GetValue<int>() ?? 0;
            if (uid <= 0)
            {
                return null;
            }

            var name = ReadString(result["name"]) is { Length: > 0 } n ? n : username;
            var isAdmin = result["is_admin"]?.GetValue<bool>() == true || result["is_system"]?.GetValue<bool>() == true;
            var role = isAdmin ? "Administrator" : "Cashier";

            // Buscar la sesión activa de POS asignada específicamente a este usuario
            var userSessions = await CallAsync("pos.session", "search_read", new
            {
                domain = new object[]
                {
                    new object[] { "state", "=", "opened" },
                    new object[] { "user_id", "=", uid }
                },
                fields = new[] { "id", "name", "state", "user_id", "config_id" },
                limit = 1
            }, cancellationToken);

            var sessionRow = userSessions?.AsArray().FirstOrDefault();

            int? posSessionId = null;
            string? posConfigName = "Sin Caja Asignada";

            if (sessionRow != null)
            {
                posSessionId = sessionRow["id"]!.GetValue<int>();
                var (_, configName) = ReadMany2One(sessionRow["config_id"]);
                var sessionName = ReadString(sessionRow["name"]);

                posConfigName = !string.IsNullOrWhiteSpace(configName)
                    ? configName
                    : (!string.IsNullOrWhiteSpace(sessionName) ? sessionName : "Caja Principal");
            }

            return new OdooUserAuthDto(uid, name, username, role, posSessionId, posConfigName);
        }
    }

    // ------------------------------------------------------------------
    // Sesión POS (Sección 14)
    // ------------------------------------------------------------------

    public async Task<PosSessionDto?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallAsync("pos.session", "search_read", new
        {
            domain = new object[] { new object[] { "state", "=", "opened" } },
            fields = new[] { "id", "name", "state", "user_id" },
            limit = 1
        }, cancellationToken);

        var first = result?.AsArray().FirstOrDefault();
        if (first is null) return null;

        var state = ReadString(first["state"]);
        var (userId, userName) = ReadMany2One(first["user_id"]);

        return new PosSessionDto(first["id"]!.GetValue<int>(), ReadString(first["name"]), state, userId, state == "opened", userName);
    }

    public async Task<PosSessionDto?> GetActiveSessionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var result = await CallAsync("pos.session", "search_read", new
        {
            domain = new object[]
            {
                new object[] { "id", "=", sessionId },
                new object[] { "state", "=", "opened" }
            },
            fields = new[] { "id", "name", "state", "user_id" },
            limit = 1
        }, cancellationToken);

        var first = result?.AsArray().FirstOrDefault();
        if (first is null) return null;

        var state = ReadString(first["state"]);
        var (userId, userName) = ReadMany2One(first["user_id"]);

        return new PosSessionDto(first["id"]!.GetValue<int>(), ReadString(first["name"]), state, userId, state == "opened", userName);
    }

    // ------------------------------------------------------------------
    // Registro de la venta (Sección 13, RN-07)
    // ------------------------------------------------------------------

    public async Task<OdooSyncResultDto> SyncSaleOrderAsync(SaleOrder saleOrder, CancellationToken cancellationToken = default)
    {
        var cashMethodId = await ResolveCashPaymentMethodIdAsync(saleOrder.PosSessionId, cancellationToken);
        return await SyncSaleOrderAsync(saleOrder, cashMethodId, cancellationToken);
    }

    /// <summary>
    /// [GUÍA ESTEBAN - TURNO 3 - RF-10]:
    /// Sobrecarga que permite sincronizar la venta con cualquier método de pago resuelto polimórficamente (Strategy).
    /// </summary>
    public async Task<OdooSyncResultDto> SyncSaleOrderAsync(SaleOrder saleOrder, int paymentMethodId, CancellationToken cancellationToken = default)
    {
        try
        {
            var orderPayload = new OdooPosOrderPayloadBuilder()
                .FromSaleOrder(saleOrder)
                .WithPayment(paymentMethodId)
                .Build();

            var result = await CallAsync("pos.order", "sync_from_ui", new
            {
                orders = new[] { orderPayload }
            }, cancellationToken);

            var uuid = saleOrder.Id.ToString();
            var created = result?["pos.order"]?.AsArray()
                .FirstOrDefault(o => ReadString(o?["uuid"]) == uuid)
                ?? result?["pos.order"]?.AsArray().FirstOrDefault();

            if (created is null)
            {
                return new OdooSyncResultDto(false, string.Empty, 0, "Odoo no devolvió la orden creada en la respuesta de sync_from_ui.");
            }

            var odooId = created["id"]!.GetValue<int>();
            var state = ReadString(created["state"]);
            var reference = ReadString(created["pos_reference"]) is { Length: > 0 } pr ? pr : ReadString(created["name"]);

            // RN-07: solo se declara éxito si Odoo dejó la orden efectivamente pagada.
            if (state is not ("paid" or "done" or "invoiced"))
            {
                return new OdooSyncResultDto(false, reference, odooId, $"Odoo registró la orden #{odooId} pero en estado '{state}', no pagada.");
            }

            // Asignar explícitamente el usuario vendedor responsable a la orden en Odoo
            if (saleOrder.UserId.HasValue && saleOrder.UserId.Value > 0)
            {
                try
                {
                    await CallAsync("pos.order", "write", new
                    {
                        ids = new[] { odooId },
                        vals = new { user_id = saleOrder.UserId.Value }
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo actualizar user_id={UserId} en la orden #{OdooId} de Odoo.", saleOrder.UserId.Value, odooId);
                }
            }

            // Descontar existencias vendidas directamente en Odoo para sincronizacion inmediata de stock
            await DeductSoldProductsStockAsync(saleOrder, cancellationToken);
            return new OdooSyncResultDto(true, reference, odooId, null);
        }
        catch (OdooCommunicationException ex)
        {
            _logger.LogError(ex, "Odoo rechazó o no respondió la sincronización de la orden {OrderId}.", saleOrder.Id);
            return new OdooSyncResultDto(false, string.Empty, 0, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción inesperada sincronizando la orden {OrderId}.", saleOrder.Id);
            return new OdooSyncResultDto(false, string.Empty, 0, ex.Message);
        }
    }

    /// <summary>
    /// [GUÍA ESTEBAN - TURNO 3 - RF-10]:
    /// Resuelve dinámicamente el ID del método de pago solicitado en el POS de Odoo.
    /// </summary>
    public async Task<int> ResolvePaymentMethodIdAsync(int sessionId, string paymentMethodType, CancellationToken ct = default)
    {
        var requested = (paymentMethodType ?? string.Empty).Trim().ToLowerInvariant();

        if (requested is "transfer" or "transferencia")
        {
            return await ResolveSpecificPaymentMethodIdAsync(sessionId, "transfer", ct);
        }
        if (requested is "card" or "tarjeta")
        {
            return await ResolveSpecificPaymentMethodIdAsync(sessionId, "card", ct);
        }
        if (requested is "bank")
        {
            return await ResolveBankPaymentMethodIdAsync(sessionId, ct);
        }
        return await ResolveCashPaymentMethodIdAsync(sessionId, ct);
    }

    private async Task<int> ResolveSpecificPaymentMethodIdAsync(int sessionId, string targetCategory, CancellationToken ct)
    {
        var session = await CallAsync("pos.session", "search_read", new
        {
            domain = new object[] { new object[] { "id", "=", sessionId } },
            fields = new[] { "id", "config_id" },
            limit = 1
        }, ct);

        var first = session?.AsArray().FirstOrDefault();
        var configNode = first?["config_id"];
        var configId = ReadIdList(configNode).FirstOrDefault();
        if (configId <= 0 && configNode is JsonValue val && val.TryGetValue<int>(out var singleId))
        {
            configId = singleId;
        }

        var methods = await CallAsync("pos.payment.method", "search_read", new
        {
            domain = new object[]
            {
                new object[] { "config_ids", "in", new[] { configId } }
            },
            fields = new[] { "id", "name", "type" },
            limit = 20
        }, ct);

        var methodList = methods?.AsArray();
        if (methodList != null && methodList.Count > 0)
        {
            if (targetCategory == "transfer")
            {
                var transferMethod = methodList.FirstOrDefault(m =>
                {
                    var name = m?["name"]?.ToString() ?? string.Empty;
                    return name.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains("banco", StringComparison.OrdinalIgnoreCase);
                });
                if (transferMethod != null)
                {
                    return transferMethod["id"]!.GetValue<int>();
                }
            }
            else if (targetCategory == "card")
            {
                var cardMethod = methodList.FirstOrDefault(m =>
                {
                    var name = m?["name"]?.ToString() ?? string.Empty;
                    return name.Contains("tarjeta", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains("card", StringComparison.OrdinalIgnoreCase);
                });
                if (cardMethod != null)
                {
                    return cardMethod["id"]!.GetValue<int>();
                }
            }

            var anyBank = methodList.FirstOrDefault(m =>
                string.Equals(m?["type"]?.ToString(), "bank", StringComparison.OrdinalIgnoreCase));
            if (anyBank != null)
            {
                return anyBank["id"]!.GetValue<int>();
            }
        }

        return await ResolveCashPaymentMethodIdAsync(sessionId, ct);
    }

    private async Task<int> ResolveBankPaymentMethodIdAsync(int sessionId, CancellationToken ct)
    {
        var session = await CallAsync("pos.session", "search_read", new
        {
            domain = new object[] { new object[] { "id", "=", sessionId } },
            fields = new[] { "id", "config_id" },
            limit = 1
        }, ct);

        var first = session?.AsArray().FirstOrDefault();
        var configNode = first?["config_id"];
        var configId = ReadIdList(configNode).FirstOrDefault();
        if (configId <= 0 && configNode is JsonValue val && val.TryGetValue<int>(out var singleId))
        {
            configId = singleId;
        }

        var methods = await CallAsync("pos.payment.method", "search_read", new
        {
            domain = new object[]
            {
                new object[] { "type", "=", "bank" },
                new object[] { "config_ids", "in", new[] { configId } }
            },
            fields = new[] { "id", "name" },
            limit = 1
        }, ct);

        var method = methods?.AsArray().FirstOrDefault();
        if (method != null)
        {
            return method["id"]!.GetValue<int>();
        }

        // Si no hay método 'bank' configurado en la sesión, fallback a efectivo
        return await ResolveCashPaymentMethodIdAsync(sessionId, ct);
    }

    /// <summary>Resuelve dinámicamente el método de pago en efectivo configurado en el POS de la sesión.</summary>
    private async Task<int> ResolveCashPaymentMethodIdAsync(int sessionId, CancellationToken ct)
    {
        var session = await CallAsync("pos.session", "search_read", new
        {
            domain = new object[] { new object[] { "id", "=", sessionId } },
            fields = new[] { "id", "config_id" },
            limit = 1
        }, ct);

        var first = session?.AsArray().FirstOrDefault();
        var configNode = first?["config_id"];
        var configId = ReadIdList(configNode).FirstOrDefault();
        if (configId <= 0 && configNode is JsonValue val && val.TryGetValue<int>(out var singleId))
        {
            configId = singleId;
        }

        if (configId <= 0)
        {
            _logger.LogWarning("search_read pos.session returned: {Raw}", session?.ToJsonString());
            throw new OdooCommunicationException($"No se pudo determinar la configuración POS de la sesión {sessionId}.");
        }

        var methods = await CallAsync("pos.payment.method", "search_read", new
        {
            domain = new object[]
            {
                new object[] { "type", "=", "cash" },
                new object[] { "config_ids", "in", new[] { configId } }
            },
            fields = new[] { "id", "name" },
            limit = 1
        }, ct);

        var method = methods?.AsArray().FirstOrDefault();
        if (method is null)
        {
            // Fallback: primer método con type=cash
            var anyCash = await CallAsync("pos.payment.method", "search_read", new
            {
                domain = new object[] { new object[] { "type", "=", "cash" } },
                fields = new[] { "id", "name" },
                limit = 1
            }, ct);
            method = anyCash?.AsArray().FirstOrDefault();
        }

        if (method is null)
            throw new OdooCommunicationException($"El POS (config {configId}) no tiene un método de pago en efectivo configurado.");

        return method["id"]!.GetValue<int>();
    }

    // ------------------------------------------------------------------
    // Transporte JSON-2
    // ------------------------------------------------------------------

    private async Task<JsonNode?> CallAsync(string model, string method, object body, CancellationToken ct)
    {
        if (!_options.HasUsableApiKey)
            throw new OdooCommunicationException("La API Key de Odoo no está configurada (ODOO__APIKEY). Genérela en Odoo > Preferencias > Seguridad de la cuenta.");

        var jsonPayload = JsonSerializer.Serialize(body);
        _logger.LogInformation("JSON-2 RPC call to {Model}.{Method}. Body: {Body}", model, method, jsonPayload);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"json/2/{model}/{method}")
        {
            Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (!string.IsNullOrWhiteSpace(_options.Database))
            request.Headers.Add("X-Odoo-Database", _options.Database);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OdooCommunicationException($"No fue posible conectar con Odoo en {_options.BaseUrl} ({model}.{method}): {ex.Message}", ex);
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var message = raw;
                try
                {
                    var err = JsonNode.Parse(raw);
                    message = ReadString(err?["message"]) is { Length: > 0 } m ? m : raw;
                }
                catch (JsonException) { /* cuerpo no JSON: se usa tal cual */ }

                _logger.LogWarning("Odoo devolvió error HTTP {StatusCode} en {Model}.{Method}: {Raw}", response.StatusCode, model, method, raw);

                if ((int)response.StatusCode == 403 ||
                    message.Contains("Session user does not match", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("apikey", StringComparison.OrdinalIgnoreCase))
                {
                    throw new OdooCommunicationException("El servidor Odoo denegó el acceso por permisos o credenciales no válidas.");
                }

                if ((int)response.StatusCode == 502 || (int)response.StatusCode == 503)
                {
                    throw new OdooCommunicationException("El servidor Odoo no se encuentra disponible temporalmente. Intente más tarde.");
                }

                throw new OdooCommunicationException($"El servidor Odoo no pudo procesar la solicitud");
            }

            return string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw);
        }
    }

    // Odoo devuelve `false` en campos vacíos: estos helpers normalizan esos valores.
    private static string ReadString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    private static decimal ReadDecimal(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : 0m;

    private static IEnumerable<int> ReadIdList(JsonNode? node)
    {
        if (node is not JsonArray arr) yield break;
        foreach (var item in arr)
        {
            if (item is null) continue;
            if (item is JsonValue v)
            {
                if (v.TryGetValue<int>(out var id)) yield return id;
                else if (int.TryParse(v.ToString(), out var parsedId)) yield return parsedId;
            }
        }
    }

    private static (int Id, string Name) ReadMany2One(JsonNode? node)
    {
        if (node is JsonArray arr && arr.Count >= 2)
        {
            var id = 0;
            if (arr[0] is JsonValue vId)
            {
                if (!vId.TryGetValue<int>(out id))
                    int.TryParse(vId.ToString(), out id);
            }
            var name = arr[1] is JsonValue vName && vName.TryGetValue<string>(out var s) ? s : arr[1]?.ToString() ?? string.Empty;
            return (id, name);
        }
        if (node is JsonValue v)
        {
            if (v.TryGetValue<int>(out var singleId)) return (singleId, string.Empty);
            if (int.TryParse(v.ToString(), out var parsedId)) return (parsedId, string.Empty);
        }
        return (0, string.Empty);
    }

    /// <summary>
    /// Descuenta en Odoo las cantidades vendidas para cada producto de la orden confirmada.
    /// Garantiza la integridad del stock tanto en Odoo como en las consultas subsecuentes del POS.
    /// </summary>
    private async Task DeductSoldProductsStockAsync(SaleOrder saleOrder, CancellationToken ct)
    {
        try
        {
            var productIds = saleOrder.Lines.Select(l => l.ProductId).Distinct().ToArray();
            if (productIds.Length == 0) return;

            var prods = await CallAsync("product.product", "search_read", new
            {
                domain = new object[] { new object[] { "id", "in", productIds } },
                fields = new[] { "id", "qty_available" }
            }, ct);

            var rows = prods?.AsArray() ?? new JsonArray();
            var stockMap = new Dictionary<int, decimal>();
            foreach (var r in rows)
            {
                if (r is null) continue;
                stockMap[r["id"]!.GetValue<int>()] = ReadDecimal(r["qty_available"]);
            }

            var soldGrouped = saleOrder.Lines
                .GroupBy(l => l.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity.Value));

            foreach (var (productId, soldQty) in soldGrouped)
            {
                if (!stockMap.TryGetValue(productId, out var currentQty)) continue;
                var newQty = Math.Max(0m, currentQty - soldQty);

                await CallAsync("product.product", "write", new
                {
                    ids = new[] { productId },
                    vals = new { qty_available = newQty }
                }, ct);

                _logger.LogInformation("Stock de producto {ProductId} en Odoo descontado de {CurrentQty} a {NewQty} (-{SoldQty}).",
                    productId, currentQty, newQty, soldQty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible actualizar el stock de los productos vendidos en Odoo para la orden {OrderId}.", saleOrder.Id);
        }
    }
}
