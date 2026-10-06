using Npgsql;
using TransactionalMessaging.Core.Models;
using TransactionalMessaging.PostgreSql.Inbox;
using Xunit;

namespace TransactionalMessaging.PostgreSql.IntegrationTests.Inbox;

/// <summary>
/// Integration tests for PostgreSqlInboxStore.
/// CRITICAL: These tests verify inbox deduplication contract from SPEC.md section 18:
/// "Duplicate message delivery is detected and suppressed within the business transaction."
/// </summary>
public class PostgreSqlInboxStoreTests : IDisposable
{
    private readonly string _connectionString;
    private readonly PostgreSqlInboxStore _store;
    private bool _schemaCreated;

    public PostgreSqlInboxStoreTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("POSTGRESQL_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=TransactionalMessagingTests;Username=postgres;Password=postgres";

        _store = new PostgreSqlInboxStore(_connectionString);
    }

    [RequiresPostgreSql]
    public async Task TryReserveAsync_FirstDelivery_ReturnsTrue()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var isNew = await _store.TryReserveAsync(message, transaction);

        Assert.True(isNew, "First delivery should be reserved successfully");
    }

    [RequiresPostgreSql]
    public async Task TryReserveAsync_DuplicateDelivery_ReturnsFalse()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        // First delivery
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            await _store.TryReserveAsync(message, transaction);
            await transaction.CommitAsync();
        }

        // Duplicate delivery - same ConsumerScope and MessageId
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var isNew = await _store.TryReserveAsync(message, transaction);

            Assert.False(isNew, "Duplicate delivery should be rejected");
        }
    }

    [RequiresPostgreSql]
    public async Task TryReserveAsync_WithRolledBackTransaction_DoesNotPersist()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        // Reserve but rollback
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            await _store.TryReserveAsync(message, transaction);
            await transaction.RollbackAsync();
        }

        // Same message should be reservable again
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            var isNew = await _store.TryReserveAsync(message, transaction);

            Assert.True(isNew, "Rolled-back reservation should not prevent future delivery");
        }
    }

    [RequiresPostgreSql]
    public async Task MarkCompletedAsync_AfterReservation_UpdatesState()
    {
        await EnsureSchemaAsync();

        var message = CreateTestMessage();

        using var connection = new NpgsqlConnection(_connectionString);
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
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(
            "SELECT state FROM transactional_inbox WHERE consumer_scope = @ConsumerScope AND message_id = @MessageId",
            connection);
        command.Parameters.AddWithValue("ConsumerScope", consumerScope);
        command.Parameters.AddWithValue("MessageId", messageId);
        var state = (int)(await command.ExecuteScalarAsync() ?? -1);
        return (InboxState)state;
    }

    private async Task EnsureSchemaAsync()
    {
        if (_schemaCreated) return;

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Drop and recreate tables for clean test state
        using (var dropCmd = new NpgsqlCommand(
            @"DROP TABLE IF EXISTS transactional_inbox;
              DROP TABLE IF EXISTS transactional_outbox",
            connection))
        {
            await dropCmd.ExecuteNonQueryAsync();
        }

        // Create outbox first
        var outboxSchemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TransactionalMessaging.PostgreSql", "Schema", "Outbox.sql");

        if (File.Exists(outboxSchemaPath))
        {
            var schema = await File.ReadAllTextAsync(outboxSchemaPath);
            using var createCmd = new NpgsqlCommand(schema, connection);
            await createCmd.ExecuteNonQueryAsync();
        }

        // Create inbox
        var inboxSchemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TransactionalMessaging.PostgreSql", "Schema", "Inbox.sql");

        if (File.Exists(inboxSchemaPath))
        {
            var schema = await File.ReadAllTextAsync(inboxSchemaPath);
            using var createCmd = new NpgsqlCommand(schema, connection);
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
