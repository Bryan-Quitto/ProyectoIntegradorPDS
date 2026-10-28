using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new DomainValidationException(nameof(Amount), "El monto monetario no puede ser negativo.");
        
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.ToUpperInvariant();
    }

    public static Money Zero => new(0m);

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount, a.Currency);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount, a.Currency);
    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor, a.Currency);

    public override string ToString() => $"{Amount:F2} {Currency}";
}

public readonly record struct Quantity
{
    public int Value { get; }

    public Quantity(int value)
    {
        if (value <= 0)
            throw new DomainValidationException(nameof(Quantity), "La cantidad de productos debe ser estrictamente mayor a 0 (RN-02).");

        Value = value;
    }

    public static implicit operator int(Quantity quantity) => quantity.Value;
    public static implicit operator Quantity(int value) => new(value);

    public override string ToString() => Value.ToString();
}
