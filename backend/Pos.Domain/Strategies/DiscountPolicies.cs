using Pos.Domain.Exceptions;

namespace Pos.Domain.Strategies;

public interface IDiscountPolicy
{
    string PolicyName { get; }
    decimal CalculateDiscount(decimal subtotal);
}

public sealed class NoDiscountPolicy : IDiscountPolicy
{
    public string PolicyName => "Sin Descuento";

    public decimal CalculateDiscount(decimal subtotal) => 0m;
}

public sealed class PercentageDiscountPolicy : IDiscountPolicy
{
    public decimal Percentage { get; }
    public string PolicyName => $"Descuento Porcentual ({Percentage}%)";

    public PercentageDiscountPolicy(decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new DomainValidationException(nameof(Percentage), "El porcentaje de descuento debe estar entre 0 y 100.");
        
        Percentage = percentage;
    }

    public decimal CalculateDiscount(decimal subtotal)
    {
        if (subtotal <= 0) return 0m;
        var discount = subtotal * (Percentage / 100m);
        return decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
    }
}

public sealed class ThresholdFixedDiscountPolicy : IDiscountPolicy
{
    public decimal Threshold { get; }
    public decimal FixedDiscount { get; }
    public string PolicyName => $"Descuento Fijo de {FixedDiscount:C} superando {Threshold:C}";

    public ThresholdFixedDiscountPolicy(decimal threshold, decimal fixedDiscount)
    {
        if (threshold <= 0)
            throw new DomainValidationException(nameof(Threshold), "El umbral de compra debe ser mayor a 0.");
        if (fixedDiscount <= 0)
            throw new DomainValidationException(nameof(FixedDiscount), "El monto de descuento debe ser mayor a 0.");

        Threshold = threshold;
        FixedDiscount = fixedDiscount;
    }

    public decimal CalculateDiscount(decimal subtotal)
    {
        if (subtotal >= Threshold)
        {
            return Math.Min(FixedDiscount, subtotal);
        }
        return 0m;
    }
}
