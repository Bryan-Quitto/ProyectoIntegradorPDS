using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

public class Product
{
    public int Id { get; }
    public string Name { get; }
    public Money Price { get; }
    public string Barcode { get; }
    public decimal AvailableQuantity { get; }
    public TaxRule Tax { get; }

    public Product(int id, string name, Money price, string barcode = "", decimal availableQuantity = 0m, TaxRule? tax = null)
    {
        if (id <= 0)
            throw new DomainValidationException(nameof(Id), "El identificador del producto debe ser un entero positivo.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException(nameof(Name), "El nombre del producto no puede estar vacío.");

        Id = id;
        Name = name.Trim();
        Price = price;
        Barcode = barcode?.Trim() ?? string.Empty;
        AvailableQuantity = availableQuantity;
        Tax = tax ?? TaxRule.EcuadorVatExcluded;
    }
}

public class PosSession
{
    public int Id { get; }
    public string Name { get; }
    public string State { get; }
    public int UserId { get; }

    public bool IsOpen => string.Equals(State, "opened", StringComparison.OrdinalIgnoreCase);

    public PosSession(int id, string name, string state, int userId)
    {
        if (id <= 0)
            throw new DomainValidationException(nameof(Id), "El ID de sesión de POS debe ser positivo.");

        Id = id;
        Name = name ?? string.Empty;
        State = state ?? string.Empty;
        UserId = userId;
    }
}

public class SaleOrderLine
{
    public int ProductId { get; }
    public string ProductName { get; }
    public Quantity Quantity { get; private set; }
    public Money UnitPrice { get; }
    public TaxRule Tax { get; }
    public Money SubTotal => new(UnitPrice.Amount * Quantity.Value, UnitPrice.Currency);

    public SaleOrderLine(int productId, string productName, Quantity quantity, Money unitPrice, TaxRule? tax = null)
    {
        if (productId <= 0)
            throw new DomainValidationException(nameof(ProductId), "El producto es inválido.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainValidationException(nameof(ProductName), "La descripción del producto es obligatoria.");

        ProductId = productId;
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        Tax = tax ?? TaxRule.EcuadorVatExcluded;
    }

    internal void IncreaseQuantity(Quantity additional)
    {
        Quantity = new Quantity(Quantity.Value + additional.Value);
    }
}
