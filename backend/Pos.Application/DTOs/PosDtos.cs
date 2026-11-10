namespace Pos.Application.DTOs;

/// <summary>
/// Producto tal como lo publica la fuente de verdad (Odoo). Los campos fiscales
/// (<paramref name="TaxRate"/>, <paramref name="PriceIncludesTax"/>, <paramref name="TaxIds"/>)
/// provienen de <c>account.tax</c> del producto; por defecto: IVA 15% excluido del precio.
/// </summary>
public record ProductDto(
    int Id,
    string Name,
    decimal Price,
    string Barcode,
    decimal AvailableQuantity,
    string DefaultCode = "",
    decimal TaxRate = 0.15m,
    bool PriceIncludesTax = false,
    IReadOnlyList<int>? TaxIds = null
);

public record PosSessionDto(
    int Id,
    string Name,
    string State,
    int UserId,
    bool IsOpen,
    string UserName = ""
);

public record CustomerDto(
    int Id,
    string Name,
    string Vat = "",
    string Email = "",
    string Street = "",
    string Phone = ""
);

public record CreateSaleItemDto(
    int ProductId,
    int Quantity
);

public record CreateSaleCommand(
    string IdempotencyKey,
    int PosSessionId,
    string CustomerDocument,
    string CustomerName,
    string DiscountType,
    decimal DiscountValue,
    List<CreateSaleItemDto> Items,
    string PaymentMethod = "Efectivo",
    int? PartnerId = null
);

public record SaleOrderLineDto(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal SubTotal,
    string ProductCode = "",
    decimal TaxRate = 0.15m,
    decimal NetSubTotal = 0m,
    decimal TaxAmount = 0m,
    decimal TotalWithTax = 0m
);

public record SaleOrderResponseDto(
    Guid OrderId,
    int PosSessionId,
    string CustomerDocument,
    string CustomerName,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string State,
    string? OdooOrderReference,
    int? OdooOrderId,
    DateTime CreatedAtUtc,
    List<SaleOrderLineDto> Lines,
    string DiscountPolicyName = "Sin Descuento",
    string PaymentMethod = "Efectivo",
    decimal AmountPaid = 0m,
    string SessionName = "",
    string OperatorName = "Cajero POS",
    int? PartnerId = null
);

public record OdooSyncResultDto(
    bool Success,
    string OdooReference,
    int OdooOrderId,
    string? ErrorMessage
);
