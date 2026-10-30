using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Application.DTOs;
using Pos.Application.Exceptions;
using Pos.Application.Ports;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Odoo;

/// <summary>
/// Adapter (GoF Estructural): traduce el puerto <see cref="IOdooPosGateway"/> de la capa Application
/// a la API JSON-2 de Odoo 20 (<c>POST /json/2/{model}/{method}</c> con autenticación Bearer por API Key).
/// </summary>
public class OdooJson2PosAdapter : IOdooPosGateway
{
    private static readonly string[] ProductFields =
        { "id", "name", "default_code", "lst_price", "barcode", "qty_available", "taxes_id" };

    private readonly HttpClient _httpClient;
    private readonly OdooOptions _options;
    private readonly ILogger<OdooJson2PosAdapter> _logger;

    public OdooJson2PosAdapter(
        HttpClient httpClient,
        IOptions<OdooOptions> options,
        ILogger<OdooJson2PosAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }
    }

    // ------------------------------------------------------------------
    // Catálogo (RF-01, RF-02, RN-03)
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        var domain = new List<object>
        {
            new object[] { "available_in_pos", "=", true },
            new object[] { "sale_ok", "=", true }
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            domain.Add("|");
            domain.Add("|");
            domain.Add(new object[] { "name", "ilike", q });
            domain.Add(new object[] { "default_code", "ilike", q });
            domain.Add(new object[] { "barcode", "ilike", q });
        }

        return await ReadProductsAsync(domain, limit: 100, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsByIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return Array.Empty<ProductDto>();

        var domain = new List<object>
        {
            new object[] { "id", "in", productIds.ToArray() },
            new object[] { "available_in_pos", "=", true },
            new object[] { "sale_ok", "=", true }
        };

        return await ReadProductsAsync(domain, limit: productIds.Count, cancellationToken);
    }

    private async Task<IReadOnlyList<ProductDto>> ReadProductsAsync(List<object> domain, int limit, CancellationToken ct)
    {
        var result = await CallAsync("product.product", "search_read", new
        {
            domain,
            fields = ProductFields,
            limit
        }, ct);

        var rows = result?.AsArray() ?? new JsonArray();

        // Resolver la configuración fiscal (account.tax) de todos los productos en una sola llamada.
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

        return products;
    }

    private async Task<Dictionary<int, (decimal Rate, bool PriceInclude)>> ReadTaxesAsync(int[] taxIds, CancellationToken ct)
    {
        var map = new Dictionary<int, (decimal Rate, bool PriceInclude)>();
        if (taxIds.Length == 0) return map;

        var result = await CallAsync("account.tax", "search_read", new
        {
            domain = new object[] { new object[] { "id", "in", taxIds } },
            fields = new[] { "id", "amount", "amount_type", "price_include" }
        }, ct);

        foreach (var tax in result?.AsArray() ?? new JsonArray())
        {
            if (tax is null) continue;
            if (!string.Equals(ReadString(tax["amount_type"]), "percent", StringComparison.Ordinal))
            {
                _logger.LogWarning("Impuesto {TaxId} con amount_type no porcentual: se ignora en el cálculo local.", tax["id"]);
                continue;
            }

            map[tax["id"]!.GetValue<int>()] = (ReadDecimal(tax["amount"]) / 100m, tax["price_include"]?.GetValue<bool>() == true);
        }

        return map;
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
        var userId = ReadIdList(first["user_id"]).FirstOrDefault();

        return new PosSessionDto(first["id"]!.GetValue<int>(), ReadString(first["name"]), state, userId, state == "opened");
    }

    // ------------------------------------------------------------------
    // Registro de la venta (Sección 13, RN-07)
    // ------------------------------------------------------------------

    public async Task<OdooSyncResultDto> SyncSaleOrderAsync(SaleOrder saleOrder, CancellationToken cancellationToken = default)
    {
        try
        {
            var cashMethodId = await ResolveCashPaymentMethodIdAsync(saleOrder.PosSessionId, cancellationToken);

            var orderPayload = new OdooPosOrderPayloadBuilder()
                .FromSaleOrder(saleOrder)
                .WithCashPayment(cashMethodId)
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

                throw new OdooCommunicationException($"Odoo rechazó {model}.{method} (HTTP {(int)response.StatusCode}): {message}");
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
}
