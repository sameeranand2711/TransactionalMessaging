namespace TransactionalMessaging.Core.Models;

/// <summary>
/// Result of a message publication attempt to the broker.
/// </summary>
public sealed class PublicationResult
{
    /// <summary>
    /// Indicates whether the publication was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Message identifier that was published.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Error code if publication failed.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Error message if publication failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Indicates if the failure is retryable (transient transport failure).
    /// </summary>
    public bool IsRetryable { get; init; }

    public static PublicationResult Succeeded(string messageId) =>
        new() { Success = true, MessageId = messageId };

    public static PublicationResult Failed(string messageId, string errorCode, string errorMessage, bool isRetryable) =>
        new()
        {
            Success = false,
            MessageId = messageId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            IsRetryable = isRetryable
        };
}
