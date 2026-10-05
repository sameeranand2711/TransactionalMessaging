namespace TransactionalMessaging.Core.Models;

/// <summary>
/// Represents an inbox record for deduplication of incoming messages.
/// Identity: ConsumerScope + MessageId (optionally with payload fingerprint).
/// </summary>
public sealed class InboxMessage
{
    /// <summary>
    /// Scope/context identifier for the consumer.
    /// Example: "payment-service", "wallet-handler"
    /// </summary>
    public required string ConsumerScope { get; init; }

    /// <summary>
    /// Message identifier from the producer.
    /// Combined with ConsumerScope forms the deduplication key.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Optional fingerprint of the message payload to detect producer ID reuse.
    /// </summary>
    public string? PayloadFingerprint { get; init; }

    /// <summary>
    /// Current processing state of the inbox message.
    /// </summary>
    public InboxState State { get; init; }

    /// <summary>
    /// UTC timestamp when the message was first received.
    /// </summary>
    public DateTime ReceivedAt { get; init; }

    /// <summary>
    /// UTC timestamp when the message processing was completed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Number of processing attempts.
    /// </summary>
    public int AttemptCount { get; init; }

    /// <summary>
    /// Last error encountered during processing.
    /// </summary>
    public string? LastError { get; init; }
}
