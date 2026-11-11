using System.Globalization;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Odoo;

/// <summary>
/// Builder (GoF Creacional) del payload que espera <c>pos.order.sync_from_ui</c> en Odoo 20.
/// Traduce el agregado <see cref="SaleOrder"/> a la estructura de comandos ORM de Odoo
/// (<c>[0, 0, {...}]</c>) sin recalcular montos: todos los importes provienen del Dominio.
/// </summary>
public class OdooPosOrderPayloadBuilder
{
    private string _uuid = Guid.NewGuid().ToString();
    private int _sessionId;
    private decimal _amountTax;
    private decimal _amountTotal;
    private DateTime _dateOrderUtc = DateTime.UtcNow;
    private int? _paymentMethodId;
    private int? _partnerId;
    private int? _userId;
    private readonly List<Dictionary<string, object>> _lines = new();

    public OdooPosOrderPayloadBuilder WithUuid(Guid uuid)
    {
        _uuid = uuid.ToString();
        return this;
    }

    public OdooPosOrderPayloadBuilder WithSessionId(int sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public OdooPosOrderPayloadBuilder WithUserId(int? userId)
    {
        _userId = userId;
        return this;
    }

    public OdooPosOrderPayloadBuilder WithTotals(decimal amountTax, decimal amountTotal)
    {
        _amountTax = amountTax;
        _amountTotal = amountTotal;
        return this;
    }

    public OdooPosOrderPayloadBuilder WithDateOrder(DateTime utc)
    {
        _dateOrderUtc = utc;
        return this;
    }

    public OdooPosOrderPayloadBuilder WithPartnerId(int? partnerId)
    {
        _partnerId = partnerId;
        return this;
    }

    /// <summary>
    /// [GUÍA ESTEBAN - TURNO 3 - RF-10]:
    /// Asocia dinámicamente cualquier método de pago resuelto (Efectivo, Banco, etc.)
    /// </summary>
    public OdooPosOrderPayloadBuilder WithPayment(int paymentMethodId)
    {
        _paymentMethodId = paymentMethodId;
        return this;
    }

    /// <summary>Compatibilidad hacia atrás con pruebas existentes.</summary>
    public OdooPosOrderPayloadBuilder WithCashPayment(int paymentMethodId)
    {
        return WithPayment(paymentMethodId);
    }

    public OdooPosOrderPayloadBuilder AddLine(
        int productId,
        string productName,
        int quantity,
        decimal unitPrice,
        decimal discountPercent,
        decimal priceSubtotal,
        decimal priceSubtotalIncl,
        IReadOnlyList<int> taxIds)
    {
        _lines.Add(new Dictionary<string, object>
        {
            ["uuid"] = Guid.NewGuid().ToString(),
            ["product_id"] = productId,
            ["full_product_name"] = productName,
            ["qty"] = quantity,
            ["price_unit"] = unitPrice,
            // Odoo modela el descuento de línea como PORCENTAJE, no como monto.
            ["discount"] = decimal.Round(discountPercent, 6),
            ["price_subtotal"] = priceSubtotal,
            ["price_subtotal_incl"] = priceSubtotalIncl,
            ["tax_ids"] = new object[] { new object[] { 6, 0, taxIds.ToArray() } }
        });
        return this;
    }

    public OdooPosOrderPayloadBuilder FromSaleOrder(SaleOrder order)
    {
        WithUuid(order.Id)
            .WithSessionId(order.PosSessionId)
            .WithDateOrder(order.CreatedAtUtc)
            .WithTotals(order.TaxAmount, order.TotalAmount)
            .WithPartnerId(order.PartnerId)
            .WithUserId(order.UserId);

        // Los montos fiscales los determina exclusivamente el Dominio.
        foreach (var line in order.Lines)
        {
            var fiscal = order.GetLineFiscalAmounts(line);
            AddLine(
                line.ProductId,
                line.ProductName,
                line.Quantity.Value,
                line.UnitPrice.Amount,
                fiscal.DiscountPercent,
                fiscal.TaxBase,
                fiscal.TotalWithTax,
                line.Tax.ExternalTaxIds);
        }

        return this;
    }

    public Dictionary<string, object> Build()
    {
        if (_sessionId <= 0)
            throw new InvalidOperationException("El payload de Odoo requiere un session_id válido.");
        if (_lines.Count == 0)
            throw new InvalidOperationException("El payload de Odoo requiere al menos una línea.");
        if (_paymentMethodId is null)
            throw new InvalidOperationException("El payload de Odoo requiere un método de pago resuelto.");

        var date = _dateOrderUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var payload = new Dictionary<string, object>
        {
            ["uuid"] = _uuid,
            ["session_id"] = _sessionId,
            ["state"] = "paid",
            ["date_order"] = date,
            ["amount_tax"] = _amountTax,
            ["amount_total"] = _amountTotal,
            ["amount_paid"] = _amountTotal,
            ["amount_return"] = 0m,
            ["to_invoice"] = false,
            ["lines"] = _lines.Select(l => new object[] { 0, 0, l }).ToList(),
            ["payment_ids"] = new List<object>
            {
                new object[]
                {
                    0, 0, new Dictionary<string, object>
                    {
                        ["amount"] = _amountTotal,
                        ["payment_method_id"] = _paymentMethodId.Value,
                        ["payment_date"] = date
                    }
                }
            }
        };

        if (_partnerId.HasValue && _partnerId.Value > 0)
        {
            payload["partner_id"] = _partnerId.Value;
        }

        if (_userId.HasValue && _userId.Value > 0)
        {
            payload["user_id"] = _userId.Value;
        }

        return payload;
    }
}
