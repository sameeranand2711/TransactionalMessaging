namespace TransactionalMessaging.Core.Exceptions;

/// <summary>
/// Base exception for TransactionalMessaging library errors.
/// </summary>
public class TransactionalMessagingException : Exception
{
    public TransactionalMessagingException(string message) : base(message)
    {
    }

    public TransactionalMessagingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
