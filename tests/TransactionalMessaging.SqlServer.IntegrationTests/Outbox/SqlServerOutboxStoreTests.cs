using Microsoft.Data.SqlClient;
using TransactionalMessaging.Core.Models;
using TransactionalMessaging.SqlServer.Outbox;
using Xunit;

namespace TransactionalMessaging.SqlServer.IntegrationTests.Outbox;

/// <summary>
/// Integration tests for SqlServerOutboxStore.
/// CRITICAL: These tests verify the core invariant from SPEC.md section 25:
/// "A rolled-back business transaction exposes no publishable outbox message."
/// "A committed business transaction with an outbox insert retains durable publication intent."
/// </summary>
public class SqlServerOutboxStoreTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqlServerOutboxStore _store;
    private bool _schemaCreated;

    public SqlServerOutboxStoreTests()
    {
        // Use LocalDB or configurable connection string
        _connectionString = Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=TransactionalMessagingTests;Integrated Security=true;TrustServerCertificate=true";

        _store = new SqlServerOutboxStore(_connectionString);
    }

    [RequiresSqlServer]
    public async Task WriteAsync_WithCommittedTransaction_PersistsMessage()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await _store.WriteAsync(message, transaction);
        await transaction.CommitAsync();

        // Verify message was persisted
        var exists = await MessageExistsAsync(message.MessageId);
        Assert.True(exists, "Message should exist after committed transaction");
    }

    [RequiresSqlServer]
    public async Task WriteAsync_WithRolledBackTransaction_DoesNotPersistMessage()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await _store.WriteAsync(message, transaction);
        await transaction.RollbackAsync();

        // Verify message was NOT persisted - CRITICAL invariant
        var exists = await MessageExistsAsync(message.MessageId);
        Assert.False(exists, "Message should NOT exist after rolled-back transaction");
    }

    [RequiresSqlServer]
    public async Task ClaimBatchAsync_WithPendingMessages_ClaimsSuccessfully()
    {
        await EnsureSchemaAsync();

        // Insert pending messages
        var message1 = CreateTestMessage();
        var message2 = CreateTestMessage();
        await InsertMessageAsync(message1);
        await InsertMessageAsync(message2);

        var result = await _store.ClaimBatchAsync(
            DateTime.UtcNow,
            "test-worker",
            10,
            TimeSpan.FromMinutes(2));

        Assert.Equal(2, result.Messages.Count);
        Assert.NotEmpty(result.ClaimToken);
        Assert.Equal("test-worker", result.WorkerId);
    }

    [RequiresSqlServer]
    public async Task MarkPublishedAsync_WithValidClaimToken_ReturnsTrue()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();
        await InsertMessageAsync(message);

        var claimResult = await _store.ClaimBatchAsync(
            DateTime.UtcNow,
            "test-worker",
            1,
            TimeSpan.FromMinutes(2));

        var claimed = claimResult.Messages.First();
        var success = await _store.MarkPublishedAsync(
            claimed.MessageId,
            claimResult.ClaimToken,
            DateTime.UtcNow);

        Assert.True(success);
    }

    [RequiresSqlServer]
    public async Task MarkPublishedAsync_WithInvalidClaimToken_ReturnsFalse()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();
        await InsertMessageAsync(message);

        var claimResult = await _store.ClaimBatchAsync(
            DateTime.UtcNow,
            "test-worker",
            1,
            TimeSpan.FromMinutes(2));

        var claimed = claimResult.Messages.First();

        // Use wrong claim token - should fail due to fencing
        var success = await _store.MarkPublishedAsync(
            claimed.MessageId,
            "wrong-token",
            DateTime.UtcNow);

        Assert.False(success, "Should fail with invalid claim token - fencing protection");
    }

    private static OutboxMessage CreateTestMessage() => new()
    {
        MessageId = Guid.NewGuid().ToString("N"),
        MessageType = "test.event",
        MessageVersion = "1",
        Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
        ContentType = "application/json",
        State = OutboxState.Pending,
        AttemptCount = 0,
        OccurredAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    private async Task InsertMessageAsync(OutboxMessage message)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        await _store.WriteAsync(message, transaction);
        await transaction.CommitAsync();
    }

    private async Task<bool> MessageExistsAsync(string messageId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(
            "SELECT COUNT(1) FROM [dbo].[TransactionalOutbox] WHERE [MessageId] = @MessageId",
            connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        var count = (int)(await command.ExecuteScalarAsync() ?? 0);
        return count > 0;
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

        // Drop and recreate table for clean test state
        using var dropCmd = new SqlCommand(
            "IF OBJECT_ID('[dbo].[TransactionalOutbox]', 'U') IS NOT NULL DROP TABLE [dbo].[TransactionalOutbox]",
            connection);
        await dropCmd.ExecuteNonQueryAsync();

        // Read and execute schema script
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TransactionalMessaging.SqlServer", "Schema", "Outbox.sql");

        if (File.Exists(schemaPath))
        {
            var schema = await File.ReadAllTextAsync(schemaPath);
            using var createCmd = new SqlCommand(schema, connection);
            await createCmd.ExecuteNonQueryAsync();
        }
        else
        {
            throw new InvalidOperationException($"Schema file not found: {schemaPath}");
        }

        _schemaCreated = true;
    }

    public void Dispose()
    {
        // Cleanup: drop test database
        try
        {
            var dbName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
            var masterConnectionString = _connectionString.Replace(dbName, "master");

            using var connection = new SqlConnection(masterConnectionString);
            connection.Open();

            using var cmd = new SqlCommand(
                $@"IF EXISTS(SELECT * FROM sys.databases WHERE name = '{dbName}')
                BEGIN
                    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{dbName}];
                END",
                connection);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
