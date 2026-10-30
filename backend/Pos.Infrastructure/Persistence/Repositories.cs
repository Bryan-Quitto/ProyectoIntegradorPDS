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

    public async Task<IReadOnlyList<SaleOrderResponseDto>> GetAllSalesAsync(CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.SaleAuditRecords
            .AsNoTracking()
            .OrderByDescending(s => s.SyncedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(MapToDto).ToList();
    }

    public async Task<SaleOrderResponseDto?> GetSaleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.SaleAuditRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == id, cancellationToken);

        return record != null ? MapToDto(record) : null;
    }

    private static SaleOrderResponseDto MapToDto(SaleAuditRecord record)
    {
        var rawLines = JsonSerializer.Deserialize<List<SaleOrderLineDto>>(record.OrderLinesJson) ?? new();

        var lines = rawLines.Select(l =>
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
