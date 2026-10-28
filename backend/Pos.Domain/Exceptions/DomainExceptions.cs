namespace Pos.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class DomainValidationException : DomainException
{
    public string PropertyName { get; }

    public DomainValidationException(string propertyName, string message)
        : base(message)
    {
        PropertyName = propertyName;
    }
}

public class SaleOrderStateException : DomainException
{
    public SaleOrderStateException(string message) : base(message) { }
}
