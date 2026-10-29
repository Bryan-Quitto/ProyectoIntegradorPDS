using Pos.Domain.Entities;

namespace Pos.Infrastructure.Odoo;

public class OdooPosOrderPayloadBuilder
{
    private int _sessionId;
    private string _customerDocument = "9999999999999";
    private string _customerName = "Consumidor Final";
    private string _posReference = string.Empty;
    private decimal _amountTax;
    private decimal _amountTotal;
    private readonly List<Dictionary<string, object>> _lines = new();

    public OdooPosOrderPayloadBuilder WithSessionId(int sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public OdooPosOrderPayloadBuilder WithCustomer(string document, string name)
    {
        _customerDocument = string.IsNullOrWhiteSpace(document) ? "9999999999999" : document;
        _customerName = string.IsNullOrWhiteSpace(name) ? "Consumidor Final" : name;
        return this;
    }

    public OdooPosOrderPayloadBuilder WithPosReference(string reference)
    {
        _posReference = reference;
        return this;
    }

    public OdooPosOrderPayloadBuilder WithTotals(decimal amountTax, decimal amountTotal)
    {
        _amountTax = amountTax;
        _amountTotal = amountTotal;
        return this;
    }

    public OdooPosOrderPayloadBuilder AddLine(
        int productId,
        string productName,
        int quantity,
        decimal unitPrice,
        decimal discount,
        decimal priceSubtotal,
        decimal priceSubtotalIncl)
    {
        var lineDict = new Dictionary<string, object>
        {
            ["product_id"] = productId,
            ["qty"] = quantity,
            ["price_unit"] = unitPrice,
            ["discount"] = discount,
            ["price_subtotal"] = priceSubtotal,
            ["price_subtotal_incl"] = priceSubtotalIncl
        };
        _lines.Add(lineDict);
        return this;
    }

    public OdooPosOrderPayloadBuilder FromSaleOrder(SaleOrder order)
    {
        WithSessionId(order.PosSessionId)
            .WithCustomer(order.CustomerDocument, order.CustomerName)
            .WithPosReference($"Order {order.Id.ToString()[..8]}")
            .WithTotals(order.TaxAmount, order.TotalAmount);

        // Mapea los cálculos fiscales determinados exclusivamente por el Dominio
        foreach (var line in order.Lines)
        {
            var (subtotal, discount, netSubtotal, totalWithTax) = order.GetLineFiscalAmounts(line);
            AddLine(
                line.ProductId,
                line.ProductName,
                line.Quantity.Value,
                line.UnitPrice.Amount,
                discount,
                netSubtotal,
                totalWithTax
            );
        }

        return this;
    }

    public Dictionary<string, object> Build()
    {
        var formattedLines = _lines.Select(l => new object[] { 0, 0, l }).ToList();

        return new Dictionary<string, object>
        {
            ["session_id"] = _sessionId,
            ["pos_reference"] = _posReference,
            ["amount_tax"] = _amountTax,
            ["amount_total"] = _amountTotal,
            ["amount_paid"] = _amountTotal,
            ["amount_return"] = 0m,
            ["lines"] = formattedLines,
            ["to_invoice"] = false,
            ["customer_document_snapshot"] = _customerDocument,
            ["customer_name_snapshot"] = _customerName,
            ["creation_date"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }
}
