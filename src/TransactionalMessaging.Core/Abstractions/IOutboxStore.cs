using System.Data;
using System.Data.Common;
using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.Core.Abstractions;

/// <summary>
/// Provider-specific outbox storage contract.
/// Implementations handle database-specific claim semantics (FOR UPDATE SKIP LOCKED for PostgreSQL,
/// READPAST/UPDLOCK for SQL Server).
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Writes a message to the outbox within an existing database transaction.
    /// The message and business state changes commit atomically.
    /// </summary>
    Task WriteAsync(
        OutboxMessage message,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims a batch of pending messages for processing.
    /// Uses provider-specific row locking to ensure each message is claimed by only one worker.
    /// This operation completes in a short transaction and does not hold locks during publication.
    /// </summary>
    Task<ClaimResult> ClaimBatchAsync(
        DateTime now,
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as successfully published.
    /// Only succeeds if the provided claimToken matches the current claim (fencing).
    /// </summary>
    Task<bool> MarkPublishedAsync(
        string messageId,
        string claimToken,
        DateTime publishedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a message for retry after a failure.
    /// Returns to Pending state with updated attempt count and next attempt time.
    /// Only succeeds if the provided claimToken matches (fencing).
    /// </summary>
    Task<bool> ScheduleRetryAsync(
        string messageId,
        string claimToken,
        DateTime nextAttemptAt,
        int attemptCount,
        string? errorCode,
        string? errorSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a message to DeadLettered state.
    /// Dead-lettered messages remain queryable and are not auto-deleted.
    /// Only succeeds if the provided claimToken matches (fencing).
    /// </summary>
    Task<bool> MoveToDeadLetterAsync(
        string messageId,
        string claimToken,
        string? errorCode,
        string? errorSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews the lease on a claimed message to prevent it from expiring during long operations.
    /// Only succeeds if the provided claimToken matches (fencing).
    /// </summary>
    Task<bool> RenewLeaseAsync(
        string messageId,
        string claimToken,
        DateTime newExpiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes Published messages older than the specified retention period.
    /// Never deletes Pending, Claimed, or DeadLettered messages.
    /// Performs deletion in bounded batches.
    /// </summary>
    Task<int> CleanupPublishedAsync(
        DateTime olderThan,
        int batchSize,
        CancellationToken cancellationToken = default);
}
