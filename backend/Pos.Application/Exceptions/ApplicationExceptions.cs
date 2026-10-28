namespace Pos.Application.Exceptions;

public class ApplicationValidationException : Exception
{
    public string PropertyName { get; }

    public ApplicationValidationException(string propertyName, string message) : base(message)
    {
        PropertyName = propertyName;
    }
}

public class OdooCommunicationException : Exception
{
    public OdooCommunicationException(string message) : base(message) { }
    public OdooCommunicationException(string message, Exception innerException) : base(message, innerException) { }
}
