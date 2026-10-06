using Npgsql;
using TransactionalMessaging.Core.Models;
using TransactionalMessaging.PostgreSql.Outbox;
using Xunit;

namespace TransactionalMessaging.PostgreSql.IntegrationTests.Outbox;

/// <summary>
/// Integration tests for PostgreSqlOutboxStore.
/// CRITICAL: These tests verify the core invariant from SPEC.md section 25:
/// "A rolled-back business transaction exposes no publishable outbox message."
/// "A committed business transaction with an outbox insert retains durable publication intent."
/// </summary>
public class PostgreSqlOutboxStoreTests : IDisposable
{
    private readonly string _connectionString;
    private readonly PostgreSqlOutboxStore _store;
    private bool _schemaCreated;

    public PostgreSqlOutboxStoreTests()
    {
        // Use environment variable for connection string
        _connectionString = Environment.GetEnvironmentVariable("POSTGRESQL_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=TransactionalMessagingTests;Username=postgres;Password=postgres";

        _store = new PostgreSqlOutboxStore(_connectionString);
    }

    [RequiresPostgreSql]
    public async Task WriteAsync_WithCommittedTransaction_PersistsMessage()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await _store.WriteAsync(message, transaction);
        await transaction.CommitAsync();

        // Verify message was persisted
        var exists = await MessageExistsAsync(message.MessageId);
        Assert.True(exists, "Message should exist after committed transaction");
    }

    [RequiresPostgreSql]
    public async Task WriteAsync_WithRolledBackTransaction_DoesNotPersistMessage()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await _store.WriteAsync(message, transaction);
        await transaction.RollbackAsync();

        // Verify message was NOT persisted - CRITICAL invariant
        var exists = await MessageExistsAsync(message.MessageId);
        Assert.False(exists, "Message should NOT exist after rolled-back transaction");
    }

    [RequiresPostgreSql]
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

    [RequiresPostgreSql]
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

    [RequiresPostgreSql]
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

    [RequiresPostgreSql]
    public async Task ClaimBatchAsync_WithConcurrentWorkers_EachClaimsDifferentMessages()
    {
        await EnsureSchemaAsync();

        // Insert 4 pending messages
        var messages = Enumerable.Range(0, 4).Select(_ => CreateTestMessage()).ToList();
        foreach (var msg in messages)
        {
            await InsertMessageAsync(msg);
        }

        // Two workers claim concurrently - each should get 2 unique messages
        var claim1Task = _store.ClaimBatchAsync(
            DateTime.UtcNow,
            "worker-1",
            2,
            TimeSpan.FromMinutes(2));

        var claim2Task = _store.ClaimBatchAsync(
            DateTime.UtcNow,
            "worker-2",
            2,
            TimeSpan.FromMinutes(2));

        await Task.WhenAll(claim1Task, claim2Task);

        var result1 = claim1Task.Result;
        var result2 = claim2Task.Result;

        // Each should have claimed 2 messages
        Assert.Equal(2, result1.Messages.Count);
        Assert.Equal(2, result2.Messages.Count);

        // No overlap - FOR UPDATE SKIP LOCKED guarantees this
        var ids1 = result1.Messages.Select(m => m.MessageId).ToHashSet();
        var ids2 = result2.Messages.Select(m => m.MessageId).ToHashSet();
        Assert.Empty(ids1.Intersect(ids2));
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
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        await _store.WriteAsync(message, transaction);
        await transaction.CommitAsync();
    }

    private async Task<bool> MessageExistsAsync(string messageId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(
            "SELECT COUNT(1) FROM transactional_outbox WHERE message_id = @MessageId",
            connection);
        command.Parameters.AddWithValue("MessageId", messageId);
        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    private async Task EnsureSchemaAsync()
    {
        if (_schemaCreated) return;

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Drop and recreate table for clean test state
        using var dropCmd = new NpgsqlCommand(
            "DROP TABLE IF EXISTS transactional_outbox",
            connection);
        await dropCmd.ExecuteNonQueryAsync();

        // Read and execute schema script
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TransactionalMessaging.PostgreSql", "Schema", "Outbox.sql");

        if (File.Exists(schemaPath))
        {
            var schema = await File.ReadAllTextAsync(schemaPath);
            using var createCmd = new NpgsqlCommand(schema, connection);
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
        // Cleanup: drop test table
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(
                "DROP TABLE IF EXISTS transactional_outbox",
                connection);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
