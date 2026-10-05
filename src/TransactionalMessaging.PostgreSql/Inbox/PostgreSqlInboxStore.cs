using System.Data.Common;
using Npgsql;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.PostgreSql.Inbox;

/// <summary>
/// PostgreSQL implementation of IInboxStore for message deduplication.
/// SPEC.md section 18 inbox/deduplication.
/// </summary>
public sealed class PostgreSqlInboxStore : IInboxStore
{
    private readonly string _connectionString;

    public PostgreSqlInboxStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<bool> TryReserveAsync(
        InboxMessage message,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        var npgsqlTransaction = transaction as NpgsqlTransaction
            ?? throw new ArgumentException("Transaction must be a NpgsqlTransaction.", nameof(transaction));

        // Try to insert the message as Reserved
        // If duplicate key constraint fails, this is a duplicate delivery
        // PostgreSQL ON CONFLICT provides clean duplicate detection
        const string sql = @"
            INSERT INTO transactional_inbox
            (
                consumer_scope, message_id, payload_fingerprint,
                state, received_at, attempt_count
            )
            VALUES
            (
                @consumer_scope, @message_id, @payload_fingerprint,
                @state, @received_at, @attempt_count
            )
            ON CONFLICT (consumer_scope, message_id) DO NOTHING
            RETURNING consumer_scope";

        using var command = new NpgsqlCommand(sql, npgsqlTransaction.Connection, npgsqlTransaction);
        command.Parameters.AddWithValue("consumer_scope", message.ConsumerScope);
        command.Parameters.AddWithValue("message_id", message.MessageId);
        command.Parameters.AddWithValue("payload_fingerprint", (object?)message.PayloadFingerprint ?? DBNull.Value);
        command.Parameters.AddWithValue("state", (int)InboxState.Reserved);
        command.Parameters.AddWithValue("received_at", message.ReceivedAt);
        command.Parameters.AddWithValue("attempt_count", message.AttemptCount);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null; // If insert succeeded, result contains consumer_scope
    }

    public async Task MarkCompletedAsync(
        string consumerScope,
        string messageId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var npgsqlTransaction = transaction as NpgsqlTransaction
            ?? throw new ArgumentException("Transaction must be a NpgsqlTransaction.", nameof(transaction));

        const string sql = @"
            UPDATE transactional_inbox
            SET
                state = @state,
                completed_at = NOW() AT TIME ZONE 'UTC'
            WHERE
                consumer_scope = @consumer_scope
                AND message_id = @message_id";

        using var command = new NpgsqlCommand(sql, npgsqlTransaction.Connection, npgsqlTransaction);
        command.Parameters.AddWithValue("consumer_scope", consumerScope);
        command.Parameters.AddWithValue("message_id", messageId);
        command.Parameters.AddWithValue("state", (int)InboxState.Completed);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        string consumerScope,
        string messageId,
        string errorSummary,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var npgsqlTransaction = transaction as NpgsqlTransaction
            ?? throw new ArgumentException("Transaction must be a NpgsqlTransaction.", nameof(transaction));

        const string sql = @"
            UPDATE transactional_inbox
            SET
                state = @state,
                last_error = @last_error,
                attempt_count = attempt_count + 1
            WHERE
                consumer_scope = @consumer_scope
                AND message_id = @message_id";

        using var command = new NpgsqlCommand(sql, npgsqlTransaction.Connection, npgsqlTransaction);
        command.Parameters.AddWithValue("consumer_scope", consumerScope);
        command.Parameters.AddWithValue("message_id", messageId);
        command.Parameters.AddWithValue("state", (int)InboxState.Failed);
        command.Parameters.AddWithValue("last_error", errorSummary);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CleanupCompletedAsync(
        DateTime olderThan,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // SPEC.md section 22 - only delete Completed messages
        const string sql = @"
            DELETE FROM transactional_inbox
            WHERE (consumer_scope, message_id) IN (
                SELECT consumer_scope, message_id
                FROM transactional_inbox
                WHERE
                    state = 1 -- Completed only
                    AND completed_at < @older_than
                LIMIT @batch_size
            )";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("batch_size", batchSize);
        command.Parameters.AddWithValue("older_than", olderThan);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
