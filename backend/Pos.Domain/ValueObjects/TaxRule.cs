using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Regla fiscal aplicable a un producto. Se construye a partir de la configuración
/// fiscal de la fuente de verdad (ERP), sin acoplar el dominio a ella.
/// </summary>
/// <remarks>
/// <para><see cref="PriceIncludesTax"/> = true significa que el precio de lista ya contiene el impuesto
/// (caso de Odoo con "VAT 15% G" configurado como "Incluido en el precio").</para>
/// <para><see cref="ExternalTaxIds"/> son identificadores opacos del impuesto en el sistema externo;
/// el dominio solo los transporta para que el adaptador los reenvíe sin reinterpretarlos.</para>
/// </remarks>
public sealed record TaxRule
{
    public decimal Rate { get; }
    public bool PriceIncludesTax { get; }
    public IReadOnlyList<int> ExternalTaxIds { get; }

    public TaxRule(decimal rate, bool priceIncludesTax, IReadOnlyList<int>? externalTaxIds = null)
    {
        if (rate < 0m || rate > 1m)
            throw new DomainValidationException(nameof(Rate), "La tasa de impuesto debe expresarse como fracción entre 0 y 1.");

        Rate = rate;
        PriceIncludesTax = priceIncludesTax;
        ExternalTaxIds = externalTaxIds ?? Array.Empty<int>();
    }

    /// <summary>IVA Ecuador 15% sumado al precio (precio de lista sin impuesto).</summary>
    public static TaxRule EcuadorVatExcluded { get; } = new(0.15m, priceIncludesTax: false);

    /// <summary>
    /// Calcula base imponible e impuesto para un monto bruto de línea (ya descontado),
    /// redondeando a 2 decimales por línea (equivalente a "round_per_line" de Odoo).
    /// </summary>
    public (decimal TaxBase, decimal TaxAmount, decimal TotalWithTax) Apply(decimal lineAmount)
    {
        if (PriceIncludesTax)
        {
            var total = decimal.Round(lineAmount, 2, MidpointRounding.AwayFromZero);
            var taxBase = decimal.Round(total / (1m + Rate), 2, MidpointRounding.AwayFromZero);
            return (taxBase, total - taxBase, total);
        }

        var exclBase = decimal.Round(lineAmount, 2, MidpointRounding.AwayFromZero);
        var tax = decimal.Round(exclBase * Rate, 2, MidpointRounding.AwayFromZero);
        return (exclBase, tax, exclBase + tax);
    }
}
