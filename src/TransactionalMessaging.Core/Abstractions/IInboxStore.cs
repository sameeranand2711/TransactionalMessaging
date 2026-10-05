using System.Data.Common;
using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.Core.Abstractions;

/// <summary>
/// Provider-specific inbox storage contract for message deduplication.
/// Supports atomic inbox + business transaction pattern (SPEC.md section 18).
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Attempts to reserve/check an incoming message within a database transaction.
    /// Returns true if this is the first time seeing this message (reserved successfully).
    /// Returns false if the message was already processed (duplicate).
    /// This operation participates in the same transaction as business effects.
    /// </summary>
    Task<bool> TryReserveAsync(
        InboxMessage message,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an inbox message as completed within a database transaction.
    /// Should be called in the same transaction as business state mutations.
    /// </summary>
    Task MarkCompletedAsync(
        string consumerScope,
        string messageId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an inbox message as failed.
    /// </summary>
    Task MarkFailedAsync(
        string consumerScope,
        string messageId,
        string errorSummary,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes Completed inbox messages older than the specified retention period.
    /// Inbox retention must be at least as long as the maximum plausible redelivery window.
    /// Deleting too early re-enables duplicate business execution.
    /// </summary>
    Task<int> CleanupCompletedAsync(
        DateTime olderThan,
        int batchSize,
        CancellationToken cancellationToken = default);
}
