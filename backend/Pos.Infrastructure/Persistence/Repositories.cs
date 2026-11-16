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

    public async Task<(IReadOnlyList<SaleOrderResponseDto> Items, int TotalCount)> GetSalesPagedAsync(
        int? posSessionId = null,
        int page = 1,
        int pageSize = 5,
        string? search = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SaleAuditRecords.AsNoTracking();
        if (posSessionId.HasValue)
        {
            query = query.Where(s => s.PosSessionId == posSessionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var cleanFilter = filter?.ToUpperInvariant();
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
            else
            {
                query = query.Where(s => (s.OdooOrderReference != null && EF.Functions.Like(s.OdooOrderReference, $"%{term}%")) ||
                                         EF.Functions.Like(s.CustomerName, $"%{term}%") ||
                                         EF.Functions.Like(s.CustomerDocument, $"{term}%") ||
                                         EF.Functions.Like(s.OrderId.ToString(), $"{term}%"));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var records = await query
            .OrderByDescending(s => s.SyncedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = records.Select(r => MapToDto(r, includeLines: false)).ToList();
        return (items, totalCount);
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
