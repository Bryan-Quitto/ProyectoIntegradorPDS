using Pos.Domain.Exceptions;
using Pos.Domain.States;
using Pos.Domain.Strategies;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Desglose fiscal canónico de una línea. Es la única fuente de los montos que se envían al ERP,
/// evitando duplicar cálculos fiscales en la infraestructura.
/// </summary>
public readonly record struct LineFiscalBreakdown(
    decimal LineSubtotal,
    decimal DiscountPercent,
    decimal LineDiscount,
    decimal TaxBase,
    decimal TaxAmount,
    decimal TotalWithTax);

public class SaleOrder
{
    private readonly List<SaleOrderLine> _lines = new();

    public Guid Id { get; }
    public int PosSessionId { get; }
    public string CustomerDocument { get; }
    public string CustomerName { get; }
    public IDiscountPolicy DiscountPolicy { get; private set; }
    public ISaleOrderState State { get; private set; }
    public DateTime CreatedAtUtc { get; }

    public string? OdooOrderReference { get; private set; }
    public int? OdooOrderId { get; private set; }

    public IReadOnlyList<SaleOrderLine> Lines => _lines.AsReadOnly();

    /// <summary>Suma de precio de lista × cantidad (antes de descuentos).</summary>
    public decimal SubTotal => _lines.Sum(l => l.SubTotal.Amount);

    /// <summary>
    /// Porcentaje global de descuento resultante de la estrategia (Strategy) activa.
    /// Se distribuye uniformemente en las líneas (el ERP modela el descuento como % por línea).
    /// </summary>
    public decimal DiscountPercent
    {
        get
        {
            var subtotal = SubTotal;
            if (subtotal <= 0m) return 0m;
            var discount = Math.Min(DiscountPolicy.CalculateDiscount(subtotal), subtotal);
            return discount / subtotal * 100m;
        }
    }

    /// <summary>Descuento efectivo (suma de descuentos por línea ya redondeados).</summary>
    public decimal DiscountAmount => Breakdown().Sum(b => b.LineDiscount);

    /// <summary>Base imponible total (sin impuestos).</summary>
    public decimal NetSubTotal => Breakdown().Sum(b => b.TaxBase);

    public decimal TaxAmount => Breakdown().Sum(b => b.TaxAmount);

    public decimal TotalAmount => Breakdown().Sum(b => b.TotalWithTax);

    public SaleOrder(
        int posSessionId,
        string customerDocument = "9999999999999",
        string customerName = "Consumidor Final",
        IDiscountPolicy? discountPolicy = null,
        Guid? id = null)
    {
        if (posSessionId <= 0)
            throw new DomainValidationException(nameof(PosSessionId), "La sesión de POS es requerida y debe ser un ID válido.");

        Id = id is { } given && given != Guid.Empty ? given : Guid.NewGuid();
        PosSessionId = posSessionId;
        CustomerDocument = string.IsNullOrWhiteSpace(customerDocument) ? "9999999999999" : customerDocument.Trim();
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Consumidor Final" : customerName.Trim();
        DiscountPolicy = discountPolicy ?? new NoDiscountPolicy();
        State = new DraftState();
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Desglose fiscal de una línea respetando la regla fiscal del producto
    /// (impuesto incluido o excluido del precio), con redondeo por línea.
    /// </summary>
    public LineFiscalBreakdown GetLineFiscalAmounts(SaleOrderLine line)
    {
        var pct = DiscountPercent;
        var lineSubtotal = line.SubTotal.Amount;
        var discountedAmount = decimal.Round(lineSubtotal * (1m - pct / 100m), 2, MidpointRounding.AwayFromZero);
        var lineDiscount = lineSubtotal - discountedAmount;
        var (taxBase, tax, total) = line.Tax.Apply(discountedAmount);
        return new LineFiscalBreakdown(lineSubtotal, pct, lineDiscount, taxBase, tax, total);
    }

    private IEnumerable<LineFiscalBreakdown> Breakdown() => _lines.Select(GetLineFiscalAmounts);

    public void AddLine(Product product, Quantity quantity)
    {
        EnsureCanModify();

        var existing = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing != null)
        {
            existing.IncreaseQuantity(quantity);
        }
        else
        {
            _lines.Add(new SaleOrderLine(product.Id, product.Name, quantity, product.Price, product.Tax));
        }
    }

    public void AddLine(int productId, string productName, Quantity quantity, Money unitPrice, TaxRule? tax = null)
    {
        EnsureCanModify();

        var existing = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (existing != null)
        {
            existing.IncreaseQuantity(quantity);
        }
        else
        {
            _lines.Add(new SaleOrderLine(productId, productName, quantity, unitPrice, tax));
        }
    }

    public void RemoveLine(int productId)
    {
        EnsureCanModify();

        var item = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (item != null)
        {
            _lines.Remove(item);
        }
    }

    public void SetDiscountPolicy(IDiscountPolicy discountPolicy)
    {
        EnsureCanModify();
        DiscountPolicy = discountPolicy ?? new NoDiscountPolicy();
    }

    public void Confirm()
    {
        State.Confirm(this);
    }

    public void MarkAsSynced(string odooReference, int odooOrderId)
    {
        State.MarkSynced(this, odooReference, odooOrderId);
    }

    internal void TransitionToState(ISaleOrderState newState)
    {
        State = newState;
    }

    internal void SetOdooDetails(string odooReference, int odooOrderId)
    {
        OdooOrderReference = odooReference;
        OdooOrderId = odooOrderId;
    }

    private void EnsureCanModify()
    {
        if (!State.CanModifyLines)
        {
            throw new SaleOrderStateException($"No se puede modificar una venta en estado '{State.StateName}' (RN-06).");
        }
    }
}
