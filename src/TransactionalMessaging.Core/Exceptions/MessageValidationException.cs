namespace TransactionalMessaging.Core.Exceptions;

/// <summary>
/// Exception thrown when message validation fails.
/// </summary>
public sealed class MessageValidationException : TransactionalMessagingException
{
    public string? FieldName { get; }

    public MessageValidationException(string message, string? fieldName = null)
        : base(message)
    {
        FieldName = fieldName;
    }

    public MessageValidationException(string message, string? fieldName, Exception innerException)
        : base(message, innerException)
    {
        FieldName = fieldName;
    }
}
