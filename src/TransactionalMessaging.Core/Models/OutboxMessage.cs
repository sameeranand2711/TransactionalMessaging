namespace TransactionalMessaging.Core.Models;

/// <summary>
/// Represents a durable outbox message record as defined in SPEC.md section 7.
/// This is the core entity persisted in the transactional outbox table.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Globally unique message identifier within the producer's outbox domain.
    /// Must be set by application or generated as RFC-compatible UUID.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Stable logical message type name (not CLR assembly-qualified name).
    /// Example: "wallet.withdrawal.requested"
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Explicit message schema version.
    /// </summary>
    public required string MessageVersion { get; init; }

    /// <summary>
    /// Serialized message payload. Default UTF-8 JSON.
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    /// Content type of the payload. Default: "application/json; charset=utf-8"
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Optional message headers/metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Correlation identifier for tracing related messages across services.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Causation identifier - the message that caused this message to be created.
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// Optional ordering key for strict per-key ordering mode.
    /// Example: "wallet:12345"
    /// </summary>
    public string? OrderingKey { get; init; }

    /// <summary>
    /// Optional sequence number for strict per-key ordering.
    /// Application/domain owns sequence generation.
    /// </summary>
    public long? OrderingSequence { get; init; }

    /// <summary>
    /// Current lifecycle state of the message.
    /// </summary>
    public OutboxState State { get; init; }

    /// <summary>
    /// Number of publication attempts made.
    /// </summary>
    public int AttemptCount { get; init; }

    /// <summary>
    /// UTC timestamp when the next retry attempt should occur.
    /// </summary>
    public DateTime? NextAttemptAt { get; init; }

    /// <summary>
    /// Unique claim token set when message is claimed by a dispatcher.
    /// Used for fencing to prevent stale workers from mutating state.
    /// </summary>
    public string? ClaimToken { get; init; }

    /// <summary>
    /// Identifier of the dispatcher worker that claimed this message.
    /// </summary>
    public string? ClaimedBy { get; init; }

    /// <summary>
    /// UTC timestamp when the current claim/lease expires.
    /// </summary>
    public DateTime? ClaimedUntil { get; init; }

    /// <summary>
    /// UTC timestamp when the business event occurred.
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// UTC timestamp when the outbox record was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// UTC timestamp when the message was successfully published.
    /// </summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// Last error code encountered during publication attempt.
    /// </summary>
    public string? LastErrorCode { get; init; }

    /// <summary>
    /// Last error summary. Does not persist full exception stacks by default.
    /// </summary>
    public string? LastErrorSummary { get; init; }
}
