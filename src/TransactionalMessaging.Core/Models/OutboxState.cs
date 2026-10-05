namespace TransactionalMessaging.Core.Models;

/// <summary>
/// Represents the lifecycle state of an outbox message.
/// V1 uses a minimal state machine: Pending -> Claimed -> Published or DeadLettered.
/// </summary>
public enum OutboxState
{
    /// <summary>
    /// Message is awaiting dispatch. Ready to be claimed by a dispatcher.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Message is currently claimed by a dispatcher worker with an active lease.
    /// </summary>
    Claimed = 1,

    /// <summary>
    /// Message was successfully published to the broker and acknowledged.
    /// </summary>
    Published = 2,

    /// <summary>
    /// Message exceeded retry limits or encountered a non-retryable failure.
    /// Dead-lettered messages remain queryable and are not auto-deleted.
    /// </summary>
    DeadLettered = 3
}
