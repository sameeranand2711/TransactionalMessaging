using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Hosting.Extensions;
using TransactionalMessaging.Sample;

// Configuration
const string connectionString = "Server=(localdb)\\mssqllocaldb;Database=TransactionalMessagingDemo;Integrated Security=true;TrustServerCertificate=true";

Console.WriteLine("=== TransactionalMessaging Sample ===\n");
Console.WriteLine("Prerequisites:");
Console.WriteLine("  - SQL Server LocalDB installed");
Console.WriteLine("  - Run schema scripts from src/TransactionalMessaging.SqlServer/Schema/\n");

// Ensure database and tables exist
await EnsureDatabaseAsync(connectionString);

// Build host with all TransactionalMessaging services
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTransactionalMessaging()
    .UseSqlServer(connectionString)
    .AddDispatcher()
    .AddInboxCleanup();

// Register the simple in-memory publisher
builder.Services.AddSingleton<IMessagePublisher, InMemoryPublisher>();

var host = builder.Build();

// Demonstrate outbox pattern
await DemonstrateOutboxPatternAsync(host.Services, connectionString);

// Start host to run dispatcher
Console.WriteLine("\n[Host] Starting background services...");
var hostTask = host.RunAsync();

// Wait a bit for dispatcher to process
await Task.Delay(3000);

// Demonstrate inbox deduplication
await DemonstrateInboxDeduplicationAsync(host.Services, connectionString);

Console.WriteLine("\n[Host] Press Ctrl+C to stop...");
await hostTask;

static async Task EnsureDatabaseAsync(string connString)
{
    var builder = new SqlConnectionStringBuilder(connString);
    var dbName = builder.InitialCatalog;
    builder.InitialCatalog = "master";

    using var connection = new SqlConnection(builder.ConnectionString);
    await connection.OpenAsync();

    // Create database if not exists
    using var cmd = connection.CreateCommand();
    cmd.CommandText = $@"
        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'{dbName}')
        BEGIN
            CREATE DATABASE [{dbName}];
        END";
    await cmd.ExecuteNonQueryAsync();

    Console.WriteLine($"[Setup] Database '{dbName}' ready");
}

static async Task DemonstrateOutboxPatternAsync(IServiceProvider services, string connString)
{
    Console.WriteLine("\n=== Demonstrating Outbox Pattern ===\n");

    var outboxStore = services.GetRequiredService<IOutboxStore>();

    // Simulate a business transaction
    await using var connection = new SqlConnection(connString);
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    try
    {
        Console.WriteLine("[Business] Processing order...");

        // Simulate business operation (e.g., insert order)
        // ... business logic here ...

        // Add outbox message in same transaction
        var messageId = Guid.NewGuid().ToString();
        var payload = System.Text.Encoding.UTF8.GetBytes("""{"orderId": "ORD-12345", "amount": 99.99}""");

        await outboxStore.WriteAsync(new TransactionalMessaging.Core.Models.OutboxMessage
        {
            MessageId = messageId,
            MessageType = "orders.created",
            MessageVersion = "v1",
            Payload = payload,
            ContentType = "application/json; charset=utf-8",
            State = TransactionalMessaging.Core.Models.OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }, transaction);

        await transaction.CommitAsync();
        Console.WriteLine($"[Outbox] Message {messageId} added to outbox");
        Console.WriteLine("[Business] Order committed successfully");
    }
    catch
    {
        await transaction.RollbackAsync();
        Console.WriteLine("[Business] Transaction rolled back");
        throw;
    }
}

static async Task DemonstrateInboxDeduplicationAsync(IServiceProvider services, string connString)
{
    Console.WriteLine("\n=== Demonstrating Inbox Deduplication ===\n");

    var inboxStore = services.GetRequiredService<IInboxStore>();
    var messageId = Guid.NewGuid().ToString();
    const string consumerScope = "order-processor";

    // First attempt - should succeed
    await using var connection1 = new SqlConnection(connString);
    await connection1.OpenAsync();
    await using var transaction1 = await connection1.BeginTransactionAsync();

    var message1 = new TransactionalMessaging.Core.Models.InboxMessage
    {
        MessageId = messageId,
        ConsumerScope = consumerScope,
        State = TransactionalMessaging.Core.Models.InboxState.Reserved,
        ReceivedAt = DateTime.UtcNow,
        AttemptCount = 0
    };

    var reserved1 = await inboxStore.TryReserveAsync(message1, transaction1);
    if (reserved1)
    {
        Console.WriteLine($"[Inbox] First attempt: Message {messageId} reserved successfully");

        // Simulate business processing
        Console.WriteLine("[Business] Processing incoming message...");
        await Task.Delay(100);

        await inboxStore.MarkCompletedAsync(messageId, consumerScope, transaction1);
        await transaction1.CommitAsync();
        Console.WriteLine("[Inbox] First attempt: Message marked as completed");
    }

    // Second attempt (duplicate) - should be rejected
    await using var connection2 = new SqlConnection(connString);
    await connection2.OpenAsync();
    await using var transaction2 = await connection2.BeginTransactionAsync();

    var message2 = new TransactionalMessaging.Core.Models.InboxMessage
    {
        MessageId = messageId,
        ConsumerScope = consumerScope,
        State = TransactionalMessaging.Core.Models.InboxState.Reserved,
        ReceivedAt = DateTime.UtcNow,
        AttemptCount = 0
    };

    var reserved2 = await inboxStore.TryReserveAsync(message2, transaction2);
    if (!reserved2)
    {
        Console.WriteLine($"[Inbox] Second attempt: Duplicate message {messageId} rejected ✓");
        await transaction2.RollbackAsync();
    }
    else
    {
        Console.WriteLine($"[Inbox] Second attempt: ERROR - duplicate was not detected!");
        await transaction2.RollbackAsync();
    }
}
