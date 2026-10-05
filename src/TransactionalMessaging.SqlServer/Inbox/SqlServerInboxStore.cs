using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.SqlServer.Inbox;

/// <summary>
/// SQL Server implementation of IInboxStore for message deduplication.
/// SPEC.md section 18 inbox/deduplication.
/// </summary>
public sealed class SqlServerInboxStore : IInboxStore
{
    private readonly string _connectionString;

    public SqlServerInboxStore(string connectionString)
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

        var sqlTransaction = transaction as SqlTransaction
            ?? throw new ArgumentException("Transaction must be a SqlTransaction.", nameof(transaction));

        // Try to insert the message as Reserved
        // If duplicate key constraint fails, this is a duplicate delivery
        const string sql = @"
            BEGIN TRY
                INSERT INTO [dbo].[TransactionalInbox]
                (
                    [ConsumerScope], [MessageId], [PayloadFingerprint],
                    [State], [ReceivedAt], [AttemptCount]
                )
                VALUES
                (
                    @ConsumerScope, @MessageId, @PayloadFingerprint,
                    @State, @ReceivedAt, @AttemptCount
                )
                SELECT 1 AS IsNew
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() = 2627 -- Primary key violation
                    SELECT 0 AS IsNew
                ELSE
                    THROW
            END CATCH";

        using var command = new SqlCommand(sql, sqlTransaction.Connection, sqlTransaction);
        command.Parameters.AddWithValue("@ConsumerScope", message.ConsumerScope);
        command.Parameters.AddWithValue("@MessageId", message.MessageId);
        command.Parameters.AddWithValue("@PayloadFingerprint", (object?)message.PayloadFingerprint ?? DBNull.Value);
        command.Parameters.AddWithValue("@State", (int)InboxState.Reserved);
        command.Parameters.AddWithValue("@ReceivedAt", message.ReceivedAt);
        command.Parameters.AddWithValue("@AttemptCount", message.AttemptCount);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int isNew && isNew == 1;
    }

    public async Task MarkCompletedAsync(
        string consumerScope,
        string messageId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var sqlTransaction = transaction as SqlTransaction
            ?? throw new ArgumentException("Transaction must be a SqlTransaction.", nameof(transaction));

        const string sql = @"
            UPDATE [dbo].[TransactionalInbox]
            SET
                [State] = @State,
                [CompletedAt] = SYSUTCDATETIME()
            WHERE
                [ConsumerScope] = @ConsumerScope
                AND [MessageId] = @MessageId";

        using var command = new SqlCommand(sql, sqlTransaction.Connection, sqlTransaction);
        command.Parameters.AddWithValue("@ConsumerScope", consumerScope);
        command.Parameters.AddWithValue("@MessageId", messageId);
        command.Parameters.AddWithValue("@State", (int)InboxState.Completed);

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

        var sqlTransaction = transaction as SqlTransaction
            ?? throw new ArgumentException("Transaction must be a SqlTransaction.", nameof(transaction));

        const string sql = @"
            UPDATE [dbo].[TransactionalInbox]
            SET
                [State] = @State,
                [LastError] = @LastError,
                [AttemptCount] = [AttemptCount] + 1
            WHERE
                [ConsumerScope] = @ConsumerScope
                AND [MessageId] = @MessageId";

        using var command = new SqlCommand(sql, sqlTransaction.Connection, sqlTransaction);
        command.Parameters.AddWithValue("@ConsumerScope", consumerScope);
        command.Parameters.AddWithValue("@MessageId", messageId);
        command.Parameters.AddWithValue("@State", (int)InboxState.Failed);
        command.Parameters.AddWithValue("@LastError", errorSummary);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CleanupCompletedAsync(
        DateTime olderThan,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // SPEC.md section 22 - only delete Completed messages
        const string sql = @"
            DELETE TOP (@BatchSize) FROM [dbo].[TransactionalInbox]
            WHERE
                [State] = 1 -- Completed only
                AND [CompletedAt] < @OlderThan";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@OlderThan", olderThan);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
