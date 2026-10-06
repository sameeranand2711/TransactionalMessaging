using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.SqlServer.Outbox;

/// <summary>
/// SQL Server implementation of IOutboxStore using READPAST/UPDLOCK for claiming.
/// SPEC.md sections 12 and 26 - provider-specific queue semantics.
/// </summary>
public sealed class SqlServerOutboxStore : IOutboxStore
{
    private readonly string _connectionString;

    public SqlServerOutboxStore(string connectionString)
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

        var sqlTransaction = transaction as SqlTransaction
            ?? throw new ArgumentException("Transaction must be a SqlTransaction.", nameof(transaction));

        const string sql = @"
            INSERT INTO [dbo].[TransactionalOutbox]
            (
                [MessageId], [MessageType], [MessageVersion],
                [Payload], [ContentType], [Headers],
                [CorrelationId], [CausationId],
                [OrderingKey], [OrderingSequence],
                [State], [AttemptCount], [NextAttemptAt],
                [OccurredAt], [CreatedAt]
            )
            VALUES
            (
                @MessageId, @MessageType, @MessageVersion,
                @Payload, @ContentType, @Headers,
                @CorrelationId, @CausationId,
                @OrderingKey, @OrderingSequence,
                @State, @AttemptCount, @NextAttemptAt,
                @OccurredAt, @CreatedAt
            )";

        using var command = new SqlCommand(sql, sqlTransaction.Connection, sqlTransaction);

        command.Parameters.AddWithValue("@MessageId", message.MessageId);
        command.Parameters.AddWithValue("@MessageType", message.MessageType);
        command.Parameters.AddWithValue("@MessageVersion", message.MessageVersion);
        command.Parameters.AddWithValue("@Payload", message.Payload);
        command.Parameters.AddWithValue("@ContentType", message.ContentType);
        command.Parameters.AddWithValue("@Headers",
            message.Headers != null ? JsonSerializer.Serialize(message.Headers) : DBNull.Value);
        command.Parameters.AddWithValue("@CorrelationId",
            (object?)message.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CausationId",
            (object?)message.CausationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@OrderingKey",
            (object?)message.OrderingKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@OrderingSequence",
            message.OrderingSequence.HasValue ? message.OrderingSequence.Value : DBNull.Value);
        command.Parameters.AddWithValue("@State", (int)message.State);
        command.Parameters.AddWithValue("@AttemptCount", message.AttemptCount);
        command.Parameters.AddWithValue("@NextAttemptAt",
            (object?)message.NextAttemptAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@OccurredAt", message.OccurredAt);
        command.Parameters.AddWithValue("@CreatedAt", message.CreatedAt);

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

        // SQL Server claim strategy using UPDLOCK, READPAST and ROWLOCK
        // SPEC.md section 12 - provider-specific implementation
        const string sql = @"
            UPDATE TOP (@BatchSize) [dbo].[TransactionalOutbox] WITH (UPDLOCK, READPAST, ROWLOCK)
            SET
                [State] = 1, -- Claimed
                [ClaimToken] = @ClaimToken,
                [ClaimedBy] = @WorkerId,
                [ClaimedUntil] = @ClaimExpiresAt
            OUTPUT
                INSERTED.[MessageId], INSERTED.[MessageType], INSERTED.[MessageVersion],
                INSERTED.[Payload], INSERTED.[ContentType], INSERTED.[Headers],
                INSERTED.[CorrelationId], INSERTED.[CausationId],
                INSERTED.[OrderingKey], INSERTED.[OrderingSequence],
                INSERTED.[State], INSERTED.[AttemptCount], INSERTED.[NextAttemptAt],
                INSERTED.[ClaimToken], INSERTED.[ClaimedBy], INSERTED.[ClaimedUntil],
                INSERTED.[OccurredAt], INSERTED.[CreatedAt], INSERTED.[PublishedAt],
                INSERTED.[LastErrorCode], INSERTED.[LastErrorSummary]
            WHERE
                [State] = 0 -- Pending
                AND ([NextAttemptAt] IS NULL OR [NextAttemptAt] <= @Now)";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@ClaimToken", claimToken);
        command.Parameters.AddWithValue("@WorkerId", workerId);
        command.Parameters.AddWithValue("@ClaimExpiresAt", claimExpiresAt);
        command.Parameters.AddWithValue("@Now", now);

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
            UPDATE [dbo].[TransactionalOutbox]
            SET
                [State] = 2, -- Published
                [PublishedAt] = @PublishedAt,
                [ClaimToken] = NULL,
                [ClaimedBy] = NULL,
                [ClaimedUntil] = NULL
            WHERE
                [MessageId] = @MessageId
                AND [ClaimToken] = @ClaimToken";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        command.Parameters.AddWithValue("@ClaimToken", claimToken);
        command.Parameters.AddWithValue("@PublishedAt", publishedAt);

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
            UPDATE [dbo].[TransactionalOutbox]
            SET
                [State] = 0, -- Pending
                [AttemptCount] = @AttemptCount,
                [NextAttemptAt] = @NextAttemptAt,
                [LastErrorCode] = @ErrorCode,
                [LastErrorSummary] = @ErrorSummary,
                [ClaimToken] = NULL,
                [ClaimedBy] = NULL,
                [ClaimedUntil] = NULL
            WHERE
                [MessageId] = @MessageId
                AND [ClaimToken] = @ClaimToken";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        command.Parameters.AddWithValue("@ClaimToken", claimToken);
        command.Parameters.AddWithValue("@AttemptCount", attemptCount);
        command.Parameters.AddWithValue("@NextAttemptAt", nextAttemptAt);
        command.Parameters.AddWithValue("@ErrorCode", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("@ErrorSummary", (object?)errorSummary ?? DBNull.Value);

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
            UPDATE [dbo].[TransactionalOutbox]
            SET
                [State] = 3, -- DeadLettered
                [LastErrorCode] = @ErrorCode,
                [LastErrorSummary] = @ErrorSummary,
                [ClaimToken] = NULL,
                [ClaimedBy] = NULL,
                [ClaimedUntil] = NULL
            WHERE
                [MessageId] = @MessageId
                AND [ClaimToken] = @ClaimToken";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        command.Parameters.AddWithValue("@ClaimToken", claimToken);
        command.Parameters.AddWithValue("@ErrorCode", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("@ErrorSummary", (object?)errorSummary ?? DBNull.Value);

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
            UPDATE [dbo].[TransactionalOutbox]
            SET [ClaimedUntil] = @NewExpiresAt
            WHERE
                [MessageId] = @MessageId
                AND [ClaimToken] = @ClaimToken";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        command.Parameters.AddWithValue("@ClaimToken", claimToken);
        command.Parameters.AddWithValue("@NewExpiresAt", newExpiresAt);

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
            DELETE TOP (@BatchSize) FROM [dbo].[TransactionalOutbox]
            WHERE
                [State] = 2 -- Published only
                AND [PublishedAt] < @OlderThan";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@OlderThan", olderThan);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static OutboxMessage MapOutboxMessage(SqlDataReader reader)
    {
        var headersJson = reader["Headers"] as string;
        var headers = !string.IsNullOrEmpty(headersJson)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson)
            : null;

        return new OutboxMessage
        {
            MessageId = reader.GetString("MessageId"),
            MessageType = reader.GetString("MessageType"),
            MessageVersion = reader.GetString("MessageVersion"),
            Payload = (byte[])reader["Payload"],
            ContentType = reader.GetString("ContentType"),
            Headers = headers,
            CorrelationId = reader["CorrelationId"] as string,
            CausationId = reader["CausationId"] as string,
            OrderingKey = reader["OrderingKey"] as string,
            OrderingSequence = reader["OrderingSequence"] as long?,
            State = (OutboxState)reader.GetInt32("State"),
            AttemptCount = reader.GetInt32("AttemptCount"),
            NextAttemptAt = reader["NextAttemptAt"] as DateTime?,
            ClaimToken = reader["ClaimToken"] as string,
            ClaimedBy = reader["ClaimedBy"] as string,
            ClaimedUntil = reader["ClaimedUntil"] as DateTime?,
            OccurredAt = reader.GetDateTime("OccurredAt"),
            CreatedAt = reader.GetDateTime("CreatedAt"),
            PublishedAt = reader["PublishedAt"] as DateTime?,
            LastErrorCode = reader["LastErrorCode"] as string,
            LastErrorSummary = reader["LastErrorSummary"] as string
        };
    }
}

internal static class SqlDataReaderExtensions
{
    public static string GetString(this SqlDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    public static int GetInt32(this SqlDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name));
    }

    public static DateTime GetDateTime(this SqlDataReader reader, string name)
    {
        return reader.GetDateTime(reader.GetOrdinal(name));
    }
}
