using System.Data.Common;
using System.Text.Json;
using Npgsql;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.PostgreSql.Outbox;

/// <summary>
/// PostgreSQL implementation of IOutboxStore using FOR UPDATE SKIP LOCKED for claiming.
/// SPEC.md sections 12 and 26 - provider-specific queue semantics.
/// </summary>
public sealed class PostgreSqlOutboxStore : IOutboxStore
{
    private readonly string _connectionString;

    public PostgreSqlOutboxStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task WriteAsync(
        OutboxMessage message,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        var npgsqlTransaction = transaction as NpgsqlTransaction
            ?? throw new ArgumentException("Transaction must be a NpgsqlTransaction.", nameof(transaction));

        const string sql = @"
            INSERT INTO transactional_outbox
            (
                message_id, message_type, message_version,
                payload, content_type, headers,
                correlation_id, causation_id,
                ordering_key, ordering_sequence,
                state, attempt_count, next_attempt_at,
                occurred_at, created_at
            )
            VALUES
            (
                @message_id, @message_type, @message_version,
                @payload, @content_type, @headers,
                @correlation_id, @causation_id,
                @ordering_key, @ordering_sequence,
                @state, @attempt_count, @next_attempt_at,
                @occurred_at, @created_at
            )";

        using var command = new NpgsqlCommand(sql, npgsqlTransaction.Connection, npgsqlTransaction);

        command.Parameters.AddWithValue("message_id", message.MessageId);
        command.Parameters.AddWithValue("message_type", message.MessageType);
        command.Parameters.AddWithValue("message_version", message.MessageVersion);
        command.Parameters.AddWithValue("payload", message.Payload);
        command.Parameters.AddWithValue("content_type", message.ContentType);
        command.Parameters.AddWithValue("headers",
            message.Headers != null ? JsonSerializer.Serialize(message.Headers) : DBNull.Value);
        command.Parameters.AddWithValue("correlation_id", (object?)message.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("causation_id", (object?)message.CausationId ?? DBNull.Value);
        command.Parameters.AddWithValue("ordering_key", (object?)message.OrderingKey ?? DBNull.Value);
        command.Parameters.AddWithValue("ordering_sequence",
            message.OrderingSequence.HasValue ? message.OrderingSequence.Value : DBNull.Value);
        command.Parameters.AddWithValue("state", (int)message.State);
        command.Parameters.AddWithValue("attempt_count", message.AttemptCount);
        command.Parameters.AddWithValue("next_attempt_at", (object?)message.NextAttemptAt ?? DBNull.Value);
        command.Parameters.AddWithValue("occurred_at", message.OccurredAt);
        command.Parameters.AddWithValue("created_at", message.CreatedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ClaimResult> ClaimBatchAsync(
        DateTime now,
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var claimToken = Guid.NewGuid().ToString("N");
        var claimExpiresAt = now.Add(leaseDuration);

        // PostgreSQL claim strategy using FOR UPDATE SKIP LOCKED
        // SPEC.md section 12 - provider-specific implementation
        const string sql = @"
            UPDATE transactional_outbox
            SET
                state = 1, -- Claimed
                claim_token = @claim_token,
                claimed_by = @worker_id,
                claimed_until = @claim_expires_at
            FROM (
                SELECT message_id
                FROM transactional_outbox
                WHERE
                    state = 0 -- Pending
                    AND (next_attempt_at IS NULL OR next_attempt_at <= @now)
                ORDER BY created_at
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            ) AS claimed
            WHERE transactional_outbox.message_id = claimed.message_id
            RETURNING
                transactional_outbox.message_id, transactional_outbox.message_type, transactional_outbox.message_version,
                transactional_outbox.payload, transactional_outbox.content_type, transactional_outbox.headers,
                transactional_outbox.correlation_id, transactional_outbox.causation_id,
                transactional_outbox.ordering_key, transactional_outbox.ordering_sequence,
                transactional_outbox.state, transactional_outbox.attempt_count, transactional_outbox.next_attempt_at,
                transactional_outbox.claim_token, transactional_outbox.claimed_by, transactional_outbox.claimed_until,
                transactional_outbox.occurred_at, transactional_outbox.created_at, transactional_outbox.published_at,
                transactional_outbox.last_error_code, transactional_outbox.last_error_summary";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("batch_size", batchSize);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("worker_id", workerId);
        command.Parameters.AddWithValue("claim_expires_at", claimExpiresAt);
        command.Parameters.AddWithValue("now", now);

        var messages = new List<OutboxMessage>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(MapOutboxMessage(reader));
        }

        return new ClaimResult
        {
            Messages = messages,
            ClaimToken = claimToken,
            ClaimExpiresAt = claimExpiresAt,
            WorkerId = workerId
        };
    }

    public async Task<bool> MarkPublishedAsync(
        string messageId,
        string claimToken,
        DateTime publishedAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE transactional_outbox
            SET
                state = 2, -- Published
                published_at = @published_at,
                claim_token = NULL,
                claimed_by = NULL,
                claimed_until = NULL
            WHERE
                message_id = @message_id
                AND claim_token = @claim_token";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("message_id", messageId);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("published_at", publishedAt);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task<bool> ScheduleRetryAsync(
        string messageId,
        string claimToken,
        DateTime nextAttemptAt,
        int attemptCount,
        string? errorCode,
        string? errorSummary,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE transactional_outbox
            SET
                state = 0, -- Pending
                attempt_count = @attempt_count,
                next_attempt_at = @next_attempt_at,
                last_error_code = @error_code,
                last_error_summary = @error_summary,
                claim_token = NULL,
                claimed_by = NULL,
                claimed_until = NULL
            WHERE
                message_id = @message_id
                AND claim_token = @claim_token";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("message_id", messageId);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("attempt_count", attemptCount);
        command.Parameters.AddWithValue("next_attempt_at", nextAttemptAt);
        command.Parameters.AddWithValue("error_code", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("error_summary", (object?)errorSummary ?? DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task<bool> MoveToDeadLetterAsync(
        string messageId,
        string claimToken,
        string? errorCode,
        string? errorSummary,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE transactional_outbox
            SET
                state = 3, -- DeadLettered
                last_error_code = @error_code,
                last_error_summary = @error_summary,
                claim_token = NULL,
                claimed_by = NULL,
                claimed_until = NULL
            WHERE
                message_id = @message_id
                AND claim_token = @claim_token";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("message_id", messageId);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("error_code", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("error_summary", (object?)errorSummary ?? DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task<bool> RenewLeaseAsync(
        string messageId,
        string claimToken,
        DateTime newExpiresAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE transactional_outbox
            SET claimed_until = @new_expires_at
            WHERE
                message_id = @message_id
                AND claim_token = @claim_token";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("message_id", messageId);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("new_expires_at", newExpiresAt);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task<int> CleanupPublishedAsync(
        DateTime olderThan,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // SPEC.md section 22 - cleanup never touches Pending/Claimed/DeadLettered
        const string sql = @"
            DELETE FROM transactional_outbox
            WHERE message_id IN (
                SELECT message_id
                FROM transactional_outbox
                WHERE
                    state = 2 -- Published only
                    AND published_at < @older_than
                LIMIT @batch_size
            )";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("batch_size", batchSize);
        command.Parameters.AddWithValue("older_than", olderThan);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static OutboxMessage MapOutboxMessage(NpgsqlDataReader reader)
    {
        var headersJson = reader["headers"] as string;
        var headers = !string.IsNullOrEmpty(headersJson)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson)
            : null;

        return new OutboxMessage
        {
            MessageId = reader.GetString("message_id"),
            MessageType = reader.GetString("message_type"),
            MessageVersion = reader.GetString("message_version"),
            Payload = (byte[])reader["payload"],
            ContentType = reader.GetString("content_type"),
            Headers = headers,
            CorrelationId = reader["correlation_id"] as string,
            CausationId = reader["causation_id"] as string,
            OrderingKey = reader["ordering_key"] as string,
            OrderingSequence = reader["ordering_sequence"] as long?,
            State = (OutboxState)reader.GetInt32("state"),
            AttemptCount = reader.GetInt32("attempt_count"),
            NextAttemptAt = reader["next_attempt_at"] as DateTime?,
            ClaimToken = reader["claim_token"] as string,
            ClaimedBy = reader["claimed_by"] as string,
            ClaimedUntil = reader["claimed_until"] as DateTime?,
            OccurredAt = reader.GetDateTime("occurred_at"),
            CreatedAt = reader.GetDateTime("created_at"),
            PublishedAt = reader["published_at"] as DateTime?,
            LastErrorCode = reader["last_error_code"] as string,
            LastErrorSummary = reader["last_error_summary"] as string
        };
    }
}

internal static class NpgsqlDataReaderExtensions
{
    public static string GetString(this NpgsqlDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    public static int GetInt32(this NpgsqlDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name));
    }

    public static DateTime GetDateTime(this NpgsqlDataReader reader, string name)
    {
        return reader.GetDateTime(reader.GetOrdinal(name));
    }
}
