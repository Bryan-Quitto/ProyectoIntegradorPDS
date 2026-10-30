namespace Pos.Infrastructure.Persistence.Entities;

public class IdempotencyRecord
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SaleAuditRecord
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int PosSessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string OperatorName { get; set; } = "Cajero POS";
    public string CustomerDocument { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string DiscountPolicyName { get; set; } = "Sin Descuento";
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Efectivo";
    public decimal AmountPaid { get; set; }
    public string State { get; set; } = string.Empty;
    public string? OdooOrderReference { get; set; }
    public int? OdooOrderId { get; set; }
    public string OrderLinesJson { get; set; } = "[]";
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}

public class LocalOperator
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Cashier";
    public bool IsActive { get; set; } = true;
}
