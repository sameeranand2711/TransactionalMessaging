using Microsoft.Data.SqlClient;
using TransactionalMessaging.Core.Models;
using TransactionalMessaging.SqlServer.Inbox;
using Xunit;

namespace TransactionalMessaging.SqlServer.IntegrationTests.Inbox;

/// <summary>
/// Integration tests for SqlServerInboxStore.
/// CRITICAL: These tests verify inbox deduplication contract from SPEC.md section 18:
/// "Duplicate message delivery is detected and suppressed within the business transaction."
/// </summary>
public class SqlServerInboxStoreTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqlServerInboxStore _store;
    private bool _schemaCreated;

    public SqlServerInboxStoreTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=TransactionalMessagingTests;Integrated Security=true;TrustServerCertificate=true";

        _store = new SqlServerInboxStore(_connectionString);
    }

    [RequiresSqlServer]
    public async Task TryReserveAsync_FirstDelivery_ReturnsTrue()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var isNew = await _store.TryReserveAsync(message, transaction);

        Assert.True(isNew, "First delivery should be reserved successfully");
    }

    [RequiresSqlServer]
    public async Task TryReserveAsync_DuplicateDelivery_ReturnsFalse()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        // First delivery
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            await _store.TryReserveAsync(message, transaction);
            await transaction.CommitAsync();
        }

        // Duplicate delivery - same ConsumerScope and MessageId
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var isNew = await _store.TryReserveAsync(message, transaction);

            Assert.False(isNew, "Duplicate delivery should be rejected");
        }
    }

    [RequiresSqlServer]
    public async Task TryReserveAsync_WithRolledBackTransaction_DoesNotPersist()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        // Reserve but rollback
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            await _store.TryReserveAsync(message, transaction);
            await transaction.RollbackAsync();
        }

        // Same message should be reservable again
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var isNew = await _store.TryReserveAsync(message, transaction);

            Assert.True(isNew, "Rolled-back reservation should not prevent future delivery");
        }
    }

    [RequiresSqlServer]
    public async Task MarkCompletedAsync_AfterReservation_UpdatesState()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Reserve
        using (var transaction = connection.BeginTransaction())
        {
            await _store.TryReserveAsync(message, transaction);
            await transaction.CommitAsync();
        }

        // Complete
        using (var transaction = connection.BeginTransaction())
        {
            await _store.MarkCompletedAsync(
                message.ConsumerScope,
                message.MessageId,
                transaction);

            await transaction.CommitAsync();
        }

        // Verify state is Completed
        var state = await GetInboxStateAsync(message.ConsumerScope, message.MessageId);
        Assert.Equal(InboxState.Completed, state);
    }

    private static InboxMessage CreateTestMessage() => new()
    {
        ConsumerScope = "test-consumer",
        MessageId = Guid.NewGuid().ToString("N"),
        PayloadFingerprint = Guid.NewGuid().ToString("N"),
        State = InboxState.Reserved,
        ReceivedAt = DateTime.UtcNow,
        AttemptCount = 0
    };

    private async Task<InboxState> GetInboxStateAsync(string consumerScope, string messageId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(
            "SELECT [State] FROM [dbo].[TransactionalInbox] WHERE [ConsumerScope] = @ConsumerScope AND [MessageId] = @MessageId",
            connection);
        command.Parameters.AddWithValue("@ConsumerScope", consumerScope);
        command.Parameters.AddWithValue("@MessageId", messageId);
        var state = (int)(await command.ExecuteScalarAsync() ?? -1);
        return (InboxState)state;
    }

    private async Task EnsureSchemaAsync()
    {
        if (_schemaCreated) return;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Create database if it doesn't exist
        var dbName = connection.Database;
        using (var masterConnection = new SqlConnection(_connectionString.Replace(dbName, "master")))
        {
            await masterConnection.OpenAsync();
            using var checkCmd = new SqlCommand(
                $"IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = '{dbName}') CREATE DATABASE [{dbName}]",
                masterConnection);
            await checkCmd.ExecuteNonQueryAsync();
        }

        // Drop and recreate tables for clean test state
        using (var dropCmd = new SqlCommand(
            @"IF OBJECT_ID('[dbo].[TransactionalInbox]', 'U') IS NOT NULL DROP TABLE [dbo].[TransactionalInbox];
              IF OBJECT_ID('[dbo].[TransactionalOutbox]', 'U') IS NOT NULL DROP TABLE [dbo].[TransactionalOutbox]",
            connection))
        {
            await dropCmd.ExecuteNonQueryAsync();
        }

        // Create outbox first (no FK dependency, but keep consistent)
        var outboxSchemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TransactionalMessaging.SqlServer", "Schema", "Outbox.sql");

        if (File.Exists(outboxSchemaPath))
        {
            var schema = await File.ReadAllTextAsync(outboxSchemaPath);
            using var createCmd = new SqlCommand(schema, connection);
            await createCmd.ExecuteNonQueryAsync();
        }

        // Create inbox
        var inboxSchemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TransactionalMessaging.SqlServer", "Schema", "Inbox.sql");

        if (File.Exists(inboxSchemaPath))
        {
            var schema = await File.ReadAllTextAsync(inboxSchemaPath);
            using var createCmd = new SqlCommand(schema, connection);
            await createCmd.ExecuteNonQueryAsync();
        }
        else
        {
            throw new InvalidOperationException($"Schema file not found: {inboxSchemaPath}");
        }

        _schemaCreated = true;
    }

    public void Dispose()
    {
        // Cleanup handled by outbox tests
    }
}
