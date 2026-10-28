namespace Pos.Application.DTOs;

public record ProductDto(
    int Id,
    string Name,
    decimal Price,
    string Barcode,
    decimal AvailableQuantity
);

public record PosSessionDto(
    int Id,
    string Name,
    string State,
    int UserId,
    bool IsOpen
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
    List<CreateSaleItemDto> Items
);

public record SaleOrderLineDto(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal SubTotal
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
    List<SaleOrderLineDto> Lines
);

public record OdooSyncResultDto(
    bool Success,
    string OdooReference,
    int OdooOrderId,
    string? ErrorMessage
);
