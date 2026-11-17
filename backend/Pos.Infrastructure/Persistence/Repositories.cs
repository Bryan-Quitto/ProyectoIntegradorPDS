using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pos.Application.DTOs;
using Pos.Application.Ports;
using Pos.Infrastructure.Persistence.Entities;

namespace Pos.Infrastructure.Persistence;

public class SqliteIdempotencyStore : IIdempotencyStore
{
    private readonly PosDbContext _dbContext;

    public SqliteIdempotencyStore(PosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        return await _dbContext.IdempotencyRecords
            .AnyAsync(r => r.Key == idempotencyKey, cancellationToken);
    }

    public async Task SaveKeyAsync(string idempotencyKey, Guid orderId, string responseJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return;

        var existing = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey, cancellationToken);

        if (existing == null)
        {
            _dbContext.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = idempotencyKey,
                OrderId = orderId,
                ResponseJson = responseJson,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<string?> GetResponseAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var record = await _dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey, cancellationToken);

        return record?.ResponseJson;
    }
}

public class SqliteSaleHistoryRepository : ISaleHistoryRepository
{
    private readonly PosDbContext _dbContext;

    public SqliteSaleHistoryRepository(PosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveSaleAsync(SaleOrderResponseDto sale, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.SaleAuditRecords
            .FirstOrDefaultAsync(s => s.OrderId == sale.OrderId, cancellationToken);

        if (existing == null)
        {
            _dbContext.SaleAuditRecords.Add(new SaleAuditRecord
            {
                OrderId = sale.OrderId,
                PosSessionId = sale.PosSessionId,
                SessionName = sale.SessionName,
                OperatorName = sale.OperatorName,
                CustomerDocument = sale.CustomerDocument,
                CustomerName = sale.CustomerName,
                SubTotal = sale.SubTotal,
                DiscountAmount = sale.DiscountAmount,
                DiscountPolicyName = sale.DiscountPolicyName,
                TaxAmount = sale.TaxAmount,
                TotalAmount = sale.TotalAmount,
                PaymentMethod = sale.PaymentMethod,
                AmountPaid = sale.AmountPaid > 0 ? sale.AmountPaid : sale.TotalAmount,
                State = sale.State,
                OdooOrderReference = sale.OdooOrderReference,
                OdooOrderId = sale.OdooOrderId,
                OrderLinesJson = JsonSerializer.Serialize(sale.Lines),
                SyncedAtUtc = sale.CreatedAtUtc
            });
        }
        else
        {
            existing.State = sale.State;
            existing.OdooOrderReference = sale.OdooOrderReference;
            existing.OdooOrderId = sale.OdooOrderId;
            existing.DiscountPolicyName = sale.DiscountPolicyName;
            existing.PaymentMethod = sale.PaymentMethod;
            existing.AmountPaid = sale.AmountPaid > 0 ? sale.AmountPaid : sale.TotalAmount;
            existing.SessionName = string.IsNullOrWhiteSpace(existing.SessionName) ? sale.SessionName : existing.SessionName;
            existing.OperatorName = string.IsNullOrWhiteSpace(existing.OperatorName) ? sale.OperatorName : existing.OperatorName;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SaleOrderResponseDto>> GetSalesAsync(int? posSessionId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SaleAuditRecords.AsNoTracking();
        if (posSessionId.HasValue)
        {
            query = query.Where(s => s.PosSessionId == posSessionId.Value);
        }

        var records = await query
            .OrderByDescending(s => s.SyncedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return records.Select(r => MapToDto(r, includeLines: true)).ToList();
    }

    public async Task<IReadOnlyList<SaleOrderResponseDto>> GetAllSalesAsync(CancellationToken cancellationToken = default)
    {
        return await GetSalesAsync(null, cancellationToken);
    }

    private const int HighCardinalityThreshold = 20_000;
    private const int LowSkipThreshold = 10_000;

    public async Task<(IReadOnlyList<SaleOrderResponseDto> Items, int TotalCount)> GetSalesPagedAsync(
        int? posSessionId = null,
        int page = 1,
        int pageSize = 5,
        string? search = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var term = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().Replace("\0", string.Empty);
        var cleanFilter = string.IsNullOrWhiteSpace(filter)
            ? "ALL"
            : filter.Trim().ToUpperInvariant();

        if (!_dbContext.Database.IsSqlite())
        {
            return await GetSalesPagedFallbackEfAsync(posSessionId, skip, pageSize, term, cleanFilter, cancellationToken);
        }

        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            shouldClose = true;
        }

        try
        {
            int? sessionTotalCount = null;
            if (posSessionId.HasValue)
            {
                sessionTotalCount = await CountBySessionAsync(connection, posSessionId.Value, cancellationToken);
                if (sessionTotalCount == 0)
                {
                    return (Array.Empty<SaleOrderResponseDto>(), 0);
                }
            }

            if (string.IsNullOrEmpty(term))
            {
                var totalCount = sessionTotalCount ?? await CountAllRecordsAsync(connection, cancellationToken);
                if (totalCount == 0)
                {
                    return (Array.Empty<SaleOrderResponseDto>(), 0);
                }

                var pageIds = await SelectUnfilteredPageIdsAsync(connection, posSessionId, skip, pageSize, cancellationToken);
                if (pageIds.Count == 0)
                {
                    return (Array.Empty<SaleOrderResponseDto>(), totalCount);
                }

                var items = await LoadSaleSummariesByIdsAsync(pageIds, cancellationToken);
                return (items, totalCount);
            }

            int? parsedSessionId = (cleanFilter == "SESSION" || cleanFilter == "ALL") && int.TryParse(term, out var sid)
                ? sid
                : null;
            int? effectiveParsedSessionId = parsedSessionId.HasValue && (!posSessionId.HasValue || posSessionId.Value == parsedSessionId.Value)
                ? parsedSessionId
                : null;

            // Si la caja tiene pocas filas (< 20,000), IX_SaleAuditRecords_PosSessionId_SyncedAtUtc resuelve todo en < 1 ms.
            if (posSessionId.HasValue && sessionTotalCount < HighCardinalityThreshold)
            {
                var exactPredicate = BuildExactSearchPredicate(cleanFilter, effectiveParsedSessionId);
                var countSql = $"""
                    SELECT COUNT(*)
                    FROM "SaleAuditRecords" s
                    WHERE s."PosSessionId" = @posSessionId
                      AND ({exactPredicate});
                    """;
                var totalCount = await ExecuteScalarIntAsync(
                    connection,
                    countSql,
                    term,
                    posSessionId,
                    effectiveParsedSessionId,
                    cancellationToken: cancellationToken);
                if (totalCount == 0)
                {
                    return (Array.Empty<SaleOrderResponseDto>(), 0);
                }

                var pageSql = $"""
                    SELECT s."Id"
                    FROM "SaleAuditRecords" s
                    WHERE s."PosSessionId" = @posSessionId
                      AND ({exactPredicate})
                    ORDER BY s."SyncedAtUtc" DESC
                    LIMIT @pageSize OFFSET @skip;
                    """;
                var pageIds = await ExecuteIdListAsync(
                    connection,
                    pageSql,
                    term,
                    posSessionId,
                    effectiveParsedSessionId,
                    skip,
                    pageSize,
                    cancellationToken: cancellationToken);
                if (pageIds.Count == 0)
                {
                    return (Array.Empty<SaleOrderResponseDto>(), totalCount);
                }

                var items = await LoadSaleSummariesByIdsAsync(pageIds, cancellationToken);
                return (items, totalCount);
            }

            var ftsAvailable = term.Length >= 3 && await IsFtsReadyAsync(connection, cancellationToken);
            if (ftsAvailable)
            {
                var ftsQuery = BuildFtsMatchQuery(term, cleanFilter);
                var ftsProbe = await ProbeFtsCandidateCountAsync(connection, ftsQuery, cancellationToken);
                var sessionProbe = effectiveParsedSessionId.HasValue
                    ? await ProbeSessionMatchCountAsync(connection, effectiveParsedSessionId.Value, cancellationToken)
                    : 0;

                if (ftsProbe == 0 && sessionProbe == 0)
                {
                    return (Array.Empty<SaleOrderResponseDto>(), 0);
                }

                if (ftsProbe + sessionProbe < HighCardinalityThreshold)
                {
                    return await ExecuteLowCardinalityFtsPagedAsync(
                        connection,
                        ftsQuery,
                        term,
                        cleanFilter,
                        posSessionId,
                        effectiveParsedSessionId,
                        skip,
                        pageSize,
                        cancellationToken);
                }
            }

            // Alta cardinalidad (>= 20,000 candidatos) o fallback seguro para < 3 caracteres / sin FTS5
            return await ExecuteOptionAPagedAsync(
                connection,
                term,
                cleanFilter,
                posSessionId,
                sessionTotalCount,
                effectiveParsedSessionId,
                skip,
                pageSize,
                cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<(IReadOnlyList<SaleOrderResponseDto> Items, int TotalCount)> ExecuteLowCardinalityFtsPagedAsync(
        DbConnection connection,
        string ftsQuery,
        string term,
        string cleanFilter,
        int? posSessionId,
        int? effectiveParsedSessionId,
        int skip,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var unionSql = effectiveParsedSessionId.HasValue
            ? """
              SELECT rowid AS Id FROM "SaleAuditRecordsFts" WHERE "SaleAuditRecordsFts" MATCH @ftsQuery
              UNION
              SELECT "Id" FROM "SaleAuditRecords" WHERE "PosSessionId" = @parsedSessionId
              """
            : """
              SELECT rowid AS Id FROM "SaleAuditRecordsFts" WHERE "SaleAuditRecordsFts" MATCH @ftsQuery
              """;

        // +s."PosSessionId" evita que SQLite escanee millones de entradas de IX_SaleAuditRecords_PosSessionId_SyncedAtUtc
        // cuando el conjunto FTS5 ya está acotado a < 20,000 candidatos.
        var sessionFilter = posSessionId.HasValue ? """+s."PosSessionId" = @posSessionId AND """ : string.Empty;
        var exactPredicate = BuildExactSearchPredicate(cleanFilter, effectiveParsedSessionId);

        var countSql = $"""
            WITH Candidates AS (
                {unionSql}
            )
            SELECT COUNT(*)
            FROM "SaleAuditRecords" s
            INNER JOIN Candidates c ON s."Id" = c."Id"
            WHERE {sessionFilter}({exactPredicate});
            """;

        var totalCount = await ExecuteScalarIntAsync(
            connection,
            countSql,
            term,
            posSessionId,
            effectiveParsedSessionId,
            ftsQuery: ftsQuery,
            cancellationToken: cancellationToken);

        if (totalCount == 0)
        {
            return (Array.Empty<SaleOrderResponseDto>(), 0);
        }

        var pageSql = $"""
            WITH Candidates AS (
                {unionSql}
            )
            SELECT s."Id"
            FROM "SaleAuditRecords" s
            INNER JOIN Candidates c ON s."Id" = c."Id"
            WHERE {sessionFilter}({exactPredicate})
            ORDER BY +s."SyncedAtUtc" DESC
            LIMIT @pageSize OFFSET @skip;
            """;

        var pageIds = await ExecuteIdListAsync(
            connection,
            pageSql,
            term,
            posSessionId,
            effectiveParsedSessionId,
            skip,
            pageSize,
            ftsQuery: ftsQuery,
            cancellationToken: cancellationToken);

        if (pageIds.Count == 0)
        {
            return (Array.Empty<SaleOrderResponseDto>(), totalCount);
        }

        var items = await LoadSaleSummariesByIdsAsync(pageIds, cancellationToken);
        return (items, totalCount);
    }

    private async Task<(IReadOnlyList<SaleOrderResponseDto> Items, int TotalCount)> ExecuteOptionAPagedAsync(
        DbConnection connection,
        string term,
        string cleanFilter,
        int? posSessionId,
        int? sessionTotalCount,
        int? effectiveParsedSessionId,
        int skip,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var exactPredicate = BuildExactSearchPredicate(cleanFilter, effectiveParsedSessionId);
        var countSessionClause = posSessionId.HasValue
            ? (sessionTotalCount >= HighCardinalityThreshold
                ? """+s."PosSessionId" = @posSessionId AND """
                : """s."PosSessionId" = @posSessionId AND """)
            : string.Empty;

        var countSql = $"""
            SELECT COUNT(*)
            FROM "SaleAuditRecords" s
            WHERE {countSessionClause}({exactPredicate});
            """;

        var totalCount = await ExecuteScalarIntAsync(
            connection,
            countSql,
            term,
            posSessionId,
            effectiveParsedSessionId,
            cancellationToken: cancellationToken);

        if (totalCount == 0)
        {
            return (Array.Empty<SaleOrderResponseDto>(), 0);
        }

        // Si hay alta cardinalidad (>= 20,000) y skip bajo, recorrer IX_SaleAuditRecords_SyncedAtUtc
        // (o IX_SaleAuditRecords_PosSessionId_SyncedAtUtc) encuentra la página al instante.
        // Si totalCount < 20,000 o el skip es profundo, +SyncedAtUtc fuerza plan seguro sin
        // millones de lookups aleatorios en el índice B-Tree.
        var useOrderedIndexScan = totalCount >= HighCardinalityThreshold && skip < LowSkipThreshold;
        var pageSessionClause = posSessionId.HasValue
            ? (useOrderedIndexScan
                ? """s."PosSessionId" = @posSessionId AND """
                : """+s."PosSessionId" = @posSessionId AND """)
            : string.Empty;
        var orderByClause = useOrderedIndexScan
            ? """ORDER BY s."SyncedAtUtc" DESC"""
            : """ORDER BY +s."SyncedAtUtc" DESC""";

        var pageSql = $"""
            SELECT s."Id"
            FROM "SaleAuditRecords" s
            WHERE {pageSessionClause}({exactPredicate})
            {orderByClause}
            LIMIT @pageSize OFFSET @skip;
            """;

        var pageIds = await ExecuteIdListAsync(
            connection,
            pageSql,
            term,
            posSessionId,
            effectiveParsedSessionId,
            skip,
            pageSize,
            cancellationToken: cancellationToken);

        if (pageIds.Count == 0)
        {
            return (Array.Empty<SaleOrderResponseDto>(), totalCount);
        }

        var items = await LoadSaleSummariesByIdsAsync(pageIds, cancellationToken);
        return (items, totalCount);
    }

    private async Task<List<SaleOrderResponseDto>> LoadSaleSummariesByIdsAsync(
        List<int> pageIds,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.SaleAuditRecords
            .AsNoTracking()
            .Where(s => pageIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.OrderId,
                s.PosSessionId,
                s.CustomerDocument,
                s.CustomerName,
                s.SubTotal,
                s.DiscountAmount,
                s.TaxAmount,
                s.TotalAmount,
                s.State,
                s.OdooOrderReference,
                s.OdooOrderId,
                s.SyncedAtUtc,
                s.DiscountPolicyName,
                s.PaymentMethod,
                s.AmountPaid,
                s.SessionName,
                s.OperatorName
            })
            .ToListAsync(cancellationToken);

        var rowMap = rows.ToDictionary(r => r.Id);
        var items = new List<SaleOrderResponseDto>(pageIds.Count);
        foreach (var id in pageIds)
        {
            if (!rowMap.TryGetValue(id, out var record))
            {
                continue;
            }

            items.Add(new SaleOrderResponseDto(
                record.OrderId,
                record.PosSessionId,
                record.CustomerDocument,
                record.CustomerName,
                record.SubTotal,
                record.DiscountAmount,
                record.TaxAmount,
                record.TotalAmount,
                record.State,
                record.OdooOrderReference,
                record.OdooOrderId,
                DateTime.SpecifyKind(record.SyncedAtUtc, DateTimeKind.Utc),
                new List<SaleOrderLineDto>(),
                DiscountPolicyName: string.IsNullOrWhiteSpace(record.DiscountPolicyName) ? "Sin Descuento" : record.DiscountPolicyName,
                PaymentMethod: string.IsNullOrWhiteSpace(record.PaymentMethod) ? "Efectivo" : record.PaymentMethod,
                AmountPaid: record.AmountPaid > 0 ? record.AmountPaid : record.TotalAmount,
                SessionName: string.IsNullOrWhiteSpace(record.SessionName) ? $"Sesión #{record.PosSessionId}" : record.SessionName,
                OperatorName: string.IsNullOrWhiteSpace(record.OperatorName) ? "Cajero POS" : record.OperatorName
            ));
        }

        return items;
    }

    private async Task<(IReadOnlyList<SaleOrderResponseDto> Items, int TotalCount)> GetSalesPagedFallbackEfAsync(
        int? posSessionId,
        int skip,
        int pageSize,
        string? term,
        string cleanFilter,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SaleAuditRecords.AsNoTracking();
        if (posSessionId.HasValue)
        {
            query = query.Where(s => s.PosSessionId == posSessionId.Value);
        }

        if (!string.IsNullOrEmpty(term))
        {
            if (cleanFilter == "REFERENCE")
            {
                query = query.Where(s => (s.OdooOrderReference != null && EF.Functions.Like(s.OdooOrderReference, $"%{term}%")) ||
                                         EF.Functions.Like(s.OrderId.ToString(), $"{term}%"));
            }
            else if (cleanFilter == "CUSTOMER")
            {
                query = query.Where(s => EF.Functions.Like(s.CustomerName, $"%{term}%") ||
                                         EF.Functions.Like(s.CustomerDocument, $"{term}%"));
            }
            else if (cleanFilter == "SESSION")
            {
                if (int.TryParse(term, out var parsedSessionId))
                {
                    query = query.Where(s => s.PosSessionId == parsedSessionId || (s.SessionName != null && EF.Functions.Like(s.SessionName, $"%{term}%")));
                }
                else
                {
                    query = query.Where(s => s.SessionName != null && EF.Functions.Like(s.SessionName, $"%{term}%"));
                }
            }
            else if (int.TryParse(term, out var parsedSessionId))
            {
                query = query.Where(s => (s.OdooOrderReference != null && EF.Functions.Like(s.OdooOrderReference, $"%{term}%")) ||
                                         EF.Functions.Like(s.CustomerName, $"%{term}%") ||
                                         EF.Functions.Like(s.CustomerDocument, $"{term}%") ||
                                         EF.Functions.Like(s.OrderId.ToString(), $"{term}%") ||
                                         s.PosSessionId == parsedSessionId ||
                                         (s.SessionName != null && EF.Functions.Like(s.SessionName, $"%{term}%")));
            }
            else
            {
                query = query.Where(s => (s.OdooOrderReference != null && EF.Functions.Like(s.OdooOrderReference, $"%{term}%")) ||
                                         EF.Functions.Like(s.CustomerName, $"%{term}%") ||
                                         EF.Functions.Like(s.CustomerDocument, $"{term}%") ||
                                         EF.Functions.Like(s.OrderId.ToString(), $"{term}%") ||
                                         (s.SessionName != null && EF.Functions.Like(s.SessionName, $"%{term}%")));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return (Array.Empty<SaleOrderResponseDto>(), 0);
        }

        var pageIds = await query
            .OrderByDescending(s => s.SyncedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (pageIds.Count == 0)
        {
            return (Array.Empty<SaleOrderResponseDto>(), totalCount);
        }

        var items = await LoadSaleSummariesByIdsAsync(pageIds, cancellationToken);
        return (items, totalCount);
    }

    private static string BuildFtsMatchQuery(string term, string cleanFilter)
    {
        var quoted = "\"" + term.Replace("\"", "\"\"") + "\"";
        return cleanFilter switch
        {
            "REFERENCE" => $"{{OdooOrderReference OrderId}} : {quoted}",
            "CUSTOMER" => $"{{CustomerName CustomerDocument}} : {quoted}",
            "SESSION" => $"SessionName : {quoted}",
            _ => quoted
        };
    }

    private static string EscapeLikeTerm(string term)
    {
        return term
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    private static string BuildExactSearchPredicate(string cleanFilter, int? effectiveParsedSessionId)
    {
        var sessionOr = effectiveParsedSessionId.HasValue
            ? """ OR s."PosSessionId" = @parsedSessionId"""
            : string.Empty;

        return cleanFilter switch
        {
            "REFERENCE" => """
                ((s."OdooOrderReference" IS NOT NULL AND s."OdooOrderReference" LIKE @containsPattern ESCAPE '\')
                 OR s."OrderId" LIKE @prefixPattern ESCAPE '\')
                """,
            "CUSTOMER" => """
                (s."CustomerName" LIKE @containsPattern ESCAPE '\'
                 OR s."CustomerDocument" LIKE @prefixPattern ESCAPE '\')
                """,
            "SESSION" => $"""
                ((s."SessionName" IS NOT NULL AND s."SessionName" LIKE @containsPattern ESCAPE '\'){sessionOr})
                """,
            _ => $"""
                ((s."OdooOrderReference" IS NOT NULL AND s."OdooOrderReference" LIKE @containsPattern ESCAPE '\')
                 OR s."CustomerName" LIKE @containsPattern ESCAPE '\'
                 OR s."CustomerDocument" LIKE @prefixPattern ESCAPE '\'
                 OR s."OrderId" LIKE @prefixPattern ESCAPE '\'
                 OR (s."SessionName" IS NOT NULL AND s."SessionName" LIKE @containsPattern ESCAPE '\'){sessionOr})
                """
        };
    }

    private static async Task<bool> IsFtsReadyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var tableCmd = connection.CreateCommand();
        tableCmd.CommandText = """
            SELECT CASE
                WHEN EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'SaleAuditRecordsFts')
                 AND EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'SaleAuditRecordsFts_docsize')
                THEN 1 ELSE 0
            END;
            """;
        if (Convert.ToInt64(await tableCmd.ExecuteScalarAsync(cancellationToken) ?? 0L) != 1L)
        {
            return false;
        }

        await using var docCmd = connection.CreateCommand();
        docCmd.CommandText = """
            SELECT CASE
                WHEN NOT EXISTS(SELECT 1 FROM "SaleAuditRecords")
                  OR EXISTS(SELECT 1 FROM "SaleAuditRecordsFts_docsize")
                THEN 1 ELSE 0
            END;
            """;
        var result = await docCmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result ?? 0L) == 1L;
    }

    private static async Task<int> CountBySessionAsync(DbConnection connection, int posSessionId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """SELECT COUNT(*) FROM "SaleAuditRecords" WHERE "PosSessionId" = @posSessionId;""";
        AddParameter(cmd, "@posSessionId", posSessionId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task<int> CountAllRecordsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """SELECT COUNT(*) FROM "SaleAuditRecords";""";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task<List<int>> SelectUnfilteredPageIdsAsync(
        DbConnection connection,
        int? posSessionId,
        int skip,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = posSessionId.HasValue
            ? """
              SELECT "Id" FROM "SaleAuditRecords"
              WHERE "PosSessionId" = @posSessionId
              ORDER BY "SyncedAtUtc" DESC
              LIMIT @pageSize OFFSET @skip;
              """
            : """
              SELECT "Id" FROM "SaleAuditRecords"
              ORDER BY "SyncedAtUtc" DESC
              LIMIT @pageSize OFFSET @skip;
              """;
        if (posSessionId.HasValue)
        {
            AddParameter(cmd, "@posSessionId", posSessionId.Value);
        }
        AddParameter(cmd, "@pageSize", pageSize);
        AddParameter(cmd, "@skip", skip);

        var ids = new List<int>(pageSize);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt32(0));
        }
        return ids;
    }

    private static async Task<int> ProbeFtsCandidateCountAsync(
        DbConnection connection,
        string ftsQuery,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM (
                SELECT 1 FROM "SaleAuditRecordsFts"
                WHERE "SaleAuditRecordsFts" MATCH @ftsQuery
                LIMIT @limit
            );
            """;
        AddParameter(cmd, "@ftsQuery", ftsQuery);
        AddParameter(cmd, "@limit", HighCardinalityThreshold);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task<int> ProbeSessionMatchCountAsync(
        DbConnection connection,
        int parsedSessionId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM (
                SELECT 1 FROM "SaleAuditRecords"
                WHERE "PosSessionId" = @parsedSessionId
                LIMIT @limit
            );
            """;
        AddParameter(cmd, "@parsedSessionId", parsedSessionId);
        AddParameter(cmd, "@limit", HighCardinalityThreshold);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task<int> ExecuteScalarIntAsync(
        DbConnection connection,
        string sql,
        string term,
        int? posSessionId,
        int? parsedSessionId,
        string? ftsQuery = null,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindSearchParameters(cmd, term, posSessionId, parsedSessionId, ftsQuery);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task<List<int>> ExecuteIdListAsync(
        DbConnection connection,
        string sql,
        string term,
        int? posSessionId,
        int? parsedSessionId,
        int skip,
        int pageSize,
        string? ftsQuery = null,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindSearchParameters(cmd, term, posSessionId, parsedSessionId, ftsQuery);
        AddParameter(cmd, "@pageSize", pageSize);
        AddParameter(cmd, "@skip", skip);

        var ids = new List<int>(pageSize);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt32(0));
        }
        return ids;
    }

    private static void BindSearchParameters(
        DbCommand cmd,
        string term,
        int? posSessionId,
        int? parsedSessionId,
        string? ftsQuery)
    {
        var escaped = EscapeLikeTerm(term);
        AddParameter(cmd, "@containsPattern", $"%{escaped}%");
        AddParameter(cmd, "@prefixPattern", $"{escaped}%");
        if (posSessionId.HasValue)
        {
            AddParameter(cmd, "@posSessionId", posSessionId.Value);
        }
        if (parsedSessionId.HasValue)
        {
            AddParameter(cmd, "@parsedSessionId", parsedSessionId.Value);
        }
        if (!string.IsNullOrEmpty(ftsQuery))
        {
            AddParameter(cmd, "@ftsQuery", ftsQuery);
        }
    }

    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(param);
    }

    public async Task<SaleOrderResponseDto?> GetSaleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.SaleAuditRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == id, cancellationToken);

        return record != null ? MapToDto(record, includeLines: true) : null;
    }

    private static SaleOrderResponseDto MapToDto(SaleAuditRecord record, bool includeLines = true)
    {
        List<SaleOrderLineDto> lines;
        if (!includeLines)
        {
            lines = new List<SaleOrderLineDto>();
        }
        else
        {
            List<SaleOrderLineDto> rawLines;
            try
            {
                rawLines = JsonSerializer.Deserialize<List<SaleOrderLineDto>>(record.OrderLinesJson) ?? new();
            }
            catch
            {
                rawLines = new();
            }

            lines = rawLines.Select(l =>
            {
                var taxRate = l.TaxRate > 0 ? l.TaxRate : 0.15m;
                var netSubTotal = l.NetSubTotal > 0 ? l.NetSubTotal : (l.SubTotal > 0 ? decimal.Round(l.SubTotal / (1m + taxRate), 2, MidpointRounding.AwayFromZero) : 0m);
                var taxAmount = l.TaxAmount > 0 ? l.TaxAmount : (l.SubTotal - netSubTotal);
                var totalWithTax = l.TotalWithTax > 0 ? l.TotalWithTax : l.SubTotal;
                return l with
                {
                    TaxRate = taxRate,
                    NetSubTotal = netSubTotal,
                    TaxAmount = taxAmount,
                    TotalWithTax = totalWithTax
                };
            }).ToList();
        }

        return new SaleOrderResponseDto(
            record.OrderId,
            record.PosSessionId,
            record.CustomerDocument,
            record.CustomerName,
            record.SubTotal,
            record.DiscountAmount,
            record.TaxAmount,
            record.TotalAmount,
            record.State,
            record.OdooOrderReference,
            record.OdooOrderId,
            DateTime.SpecifyKind(record.SyncedAtUtc, DateTimeKind.Utc),
            lines,
            DiscountPolicyName: string.IsNullOrWhiteSpace(record.DiscountPolicyName) ? "Sin Descuento" : record.DiscountPolicyName,
            PaymentMethod: string.IsNullOrWhiteSpace(record.PaymentMethod) ? "Efectivo" : record.PaymentMethod,
            AmountPaid: record.AmountPaid > 0 ? record.AmountPaid : record.TotalAmount,
            SessionName: string.IsNullOrWhiteSpace(record.SessionName) ? $"Sesión #{record.PosSessionId}" : record.SessionName,
            OperatorName: string.IsNullOrWhiteSpace(record.OperatorName) ? "Cajero POS" : record.OperatorName
        );
    }
}
