using Pos.Domain.Exceptions;
using Pos.Domain.States;
using Pos.Domain.Strategies;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

public class SaleOrder
{
    private readonly List<SaleOrderLine> _lines = new();
    private const decimal VatRateEcuador = 0.15m;

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

    public decimal SubTotal => _lines.Sum(l => l.SubTotal.Amount);
    public decimal DiscountAmount => DiscountPolicy.CalculateDiscount(SubTotal);
    public decimal NetSubTotal => Math.Max(0m, SubTotal - DiscountAmount);
    public decimal TaxAmount => decimal.Round(NetSubTotal * VatRateEcuador, 2, MidpointRounding.AwayFromZero);
    public decimal TotalAmount => NetSubTotal + TaxAmount;
    public decimal VatRate => VatRateEcuador;

    /// <summary>
    /// Desglose canónico de montos e impuestos por línea según las reglas de negocio de Ecuador (15% IVA).
    /// Evita duplicar la lógica fiscal en infraestructura o builders externos.
    /// </summary>
    public (decimal LineSubtotal, decimal LineDiscount, decimal NetSubtotal, decimal LineTotalWithTax) GetLineFiscalAmounts(SaleOrderLine line)
    {
        decimal totalLineSubtotals = SubTotal;
        decimal discountRatio = totalLineSubtotals > 0 ? (DiscountAmount / totalLineSubtotals) : 0m;
        decimal lineDiscount = decimal.Round(line.SubTotal.Amount * discountRatio, 2, MidpointRounding.AwayFromZero);
        decimal netLineSubtotal = Math.Max(0m, line.SubTotal.Amount - lineDiscount);
        decimal lineTax = decimal.Round(netLineSubtotal * VatRateEcuador, 2, MidpointRounding.AwayFromZero);
        decimal lineTotalWithTax = netLineSubtotal + lineTax;
        return (line.SubTotal.Amount, lineDiscount, netLineSubtotal, lineTotalWithTax);
    }

    public SaleOrder(int posSessionId, string customerDocument = "9999999999999", string customerName = "Consumidor Final", IDiscountPolicy? discountPolicy = null)
    {
        if (posSessionId <= 0)
            throw new DomainValidationException(nameof(PosSessionId), "La sesión de POS es requerida y debe ser un ID válido.");

        Id = Guid.NewGuid();
        PosSessionId = posSessionId;
        CustomerDocument = string.IsNullOrWhiteSpace(customerDocument) ? "9999999999999" : customerDocument.Trim();
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Consumidor Final" : customerName.Trim();
        DiscountPolicy = discountPolicy ?? new NoDiscountPolicy();
        State = new DraftState();
        CreatedAtUtc = DateTime.UtcNow;
    }

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
            _lines.Add(new SaleOrderLine(product.Id, product.Name, quantity, product.Price));
        }
    }

    public void AddLine(int productId, string productName, Quantity quantity, Money unitPrice)
    {
        EnsureCanModify();

        var existing = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (existing != null)
        {
            existing.IncreaseQuantity(quantity);
        }
        else
        {
            _lines.Add(new SaleOrderLine(productId, productName, quantity, unitPrice));
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
