# TransactionalMessaging

A .NET library implementing the Transactional Outbox and Inbox patterns for reliable message publishing and duplicate detection.

## Overview

TransactionalMessaging ensures that messages are published reliably by storing them in the same database transaction as your business data. This eliminates the need for distributed transactions while guaranteeing that if your business operation succeeds, the message will eventually be published.

**What it does:**
- Stores outgoing messages transactionally with your business data (Outbox pattern)
- Reliably publishes messages via background dispatcher
- Prevents duplicate message processing (Inbox pattern with deduplication)
- Supports SQL Server and PostgreSQL with optimized row-level locking

**What it does NOT do:**
- Does NOT guarantee exactly-once delivery (at-least-once only)
- Does NOT provide a message broker (bring your own: Kafka, RabbitMQ, etc.)
- Does NOT handle distributed transactions across different databases
- Does NOT eliminate the need for idempotent message handlers

**Delivery guarantee:** At-least-once publication. Duplicate messages can occur after crashes. Your consumers must be idempotent.

## Install

### Core package (required)
```bash
dotnet add package TransactionalMessaging.Core
```

### Database provider (choose one)
```bash
# For SQL Server
dotnet add package TransactionalMessaging.SqlServer

# For PostgreSQL
dotnet add package TransactionalMessaging.PostgreSql
```

### Hosting package (for background dispatcher)
```bash
dotnet add package TransactionalMessaging.Hosting
```

**Framework support:** .NET 8.0 and .NET 10.0

## Quick Start

### 1. Register services

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTransactionalMessaging()
    .UseSqlServer("your-connection-string")  // or .UsePostgreSql()
    .AddDispatcher()                          // Background publisher
    .AddInboxCleanup();                       // Cleanup old processed messages

// Register your message publisher
builder.Services.AddSingleton<IMessagePublisher, YourKafkaPublisher>();

var host = builder.Build();
await host.RunAsync();
```

### 2. Apply database schema

Run the SQL scripts for your provider:

**SQL Server:** `src/TransactionalMessaging.SqlServer/Schema/CreateTables.sql`
**PostgreSQL:** `src/TransactionalMessaging.PostgreSql/Schema/CreateTables.sql`

### 3. Publish messages transactionally

```csharp
using TransactionalMessaging.Core.Abstractions;
using Microsoft.Data.SqlClient;

public class OrderService
{
    private readonly IOutboxStore _outboxStore;

    public OrderService(IOutboxStore outboxStore)
    {
        _outboxStore = outboxStore;
    }

    public async Task CreateOrderAsync(Order order)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Save business data
            await SaveOrderToDatabase(order, connection, transaction);

            // 2. Add message to outbox in SAME transaction
            var message = new OutboxMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageType = "OrderCreated",
                MessageVersion = "1.0",
                Payload = JsonSerializer.Serialize(order),
                ContentType = "application/json"
            };

            await _outboxStore.AddAsync(message, connection, transaction);

            // 3. Commit both together
            await transaction.CommitAsync();
            
            // Message will be published by background dispatcher
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### 4. Handle incoming messages with deduplication

```csharp
public class OrderConsumer
{
    private readonly IInboxStore _inboxStore;

    public async Task HandleMessageAsync(string messageId, string payload)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Reserve message (fails if duplicate)
        var inboxMessage = new InboxMessage
        {
            MessageId = messageId,
            ConsumerScope = "order-service",
            ReceivedAtUtc = DateTime.UtcNow
        };

        bool reserved = await _inboxStore.TryReserveAsync(
            inboxMessage, 
            connection, 
            transaction);

        if (!reserved)
        {
            // Duplicate message - skip processing
            return;
        }

        try
        {
            // Process the message
            await ProcessOrder(payload, connection, transaction);

            // Mark as completed
            await _inboxStore.MarkCompletedAsync(
                messageId, 
                "order-service", 
                connection, 
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

## Core Concepts

### Transactional Outbox

**Problem:** Publishing a message and updating the database are two separate operations. If your process crashes between them, data becomes inconsistent.

**Solution:** Store the message in the database in the same transaction as your business data. A background dispatcher later publishes pending messages.

**Guarantee:** If the database transaction commits, the message will eventually be published.

**Limitation:** Messages may be published multiple times if the dispatcher crashes after publishing but before marking the message as sent.

### Inbox Deduplication

**Problem:** At-least-once delivery means consumers may receive the same message multiple times.

**Solution:** Track received message IDs in a database inbox table using row-level locking. Only the first attempt to reserve a message ID succeeds.

**Consumer scope:** Messages are deduplicated per `ConsumerScope` (e.g., "order-service"), allowing multiple consumers to process the same message ID independently.

### Message States

**Outbox states:**
- `Pending` — Waiting to be published
- `Publishing` — Currently being published (claimed by dispatcher)
- `Published` — Successfully published

**Inbox states:**
- `Reserved` — Message received, processing in progress
- `Completed` — Processing finished successfully
- `Failed` — Processing failed permanently (after max retries)

### Row-Level Locking

**SQL Server:** Uses `READPAST` hint to skip locked rows
**PostgreSQL:** Uses `FOR UPDATE SKIP LOCKED` for queue-like concurrency

This allows multiple dispatcher instances to run safely without distributed locking.

## Configuration

### Message Options

```csharp
builder.Services.AddTransactionalMessaging(
    configureMessage: opts =>
    {
        opts.MaxPayloadSizeBytes = 1_048_576;  // Default: 1 MB
        opts.MaxHeaderSizeBytes = 4096;         // Default: 4 KB
    });
```

### Retry Options

```csharp
builder.Services.AddTransactionalMessaging(
    configureRetry: opts =>
    {
        opts.MaxAttempts = 5;                   // Default: 5
        opts.InitialDelaySeconds = 2;           // Default: 2s
        opts.MaxDelaySeconds = 300;             // Default: 5m
        opts.BackoffMultiplier = 2.0;           // Default: 2.0 (exponential)
    });
```

**Retry schedule:** 2s, 4s, 8s, 16s, 32s (capped at MaxDelaySeconds)

### Dispatcher Options

```csharp
builder.Services.AddTransactionalMessaging(
    configureDispatcher: opts =>
    {
        opts.PollingIntervalSeconds = 5;        // Default: 5s
        opts.BatchSize = 100;                   // Default: 100
        opts.ClaimDurationSeconds = 60;         // Default: 60s
        opts.MaxConcurrentDispatches = 10;      // Default: 10
    });
```

**Safe values:**
- Reduce `PollingIntervalSeconds` for lower latency (minimum: 1s)
- Increase `BatchSize` for higher throughput (maximum: 1000)
- Adjust `ClaimDurationSeconds` based on your publish latency
- Set `MaxConcurrentDispatches` based on available resources

## Usage

### Publishing Messages

See the complete example in `samples/TransactionalMessaging.Sample/Program.cs`:

```csharp
static async Task DemonstrateOutboxPatternAsync(IServiceProvider services, string connString)
{
    var outboxStore = services.GetRequiredService<IOutboxStore>();
    
    await using var connection = new SqlConnection(connString);
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    Console.WriteLine("[Business] Processing order...");
    
    // Simulate business operation here
    
    var message = new OutboxMessage
    {
        MessageId = Guid.NewGuid().ToString(),
        MessageType = "OrderCreated",
        MessageVersion = "1.0",
        Payload = JsonSerializer.Serialize(new { OrderId = 12345 }),
        ContentType = "application/json",
        CorrelationId = "correlation-123",
        OrderingKey = "customer-67890"
    };

    await outboxStore.AddAsync(message, connection, transaction);
    Console.WriteLine($"[Outbox] Message {message.MessageId} added to outbox");
    
    await transaction.CommitAsync();
    Console.WriteLine("[Business] Order committed successfully");
}
```

### Implementing IMessagePublisher

```csharp
public class KafkaMessagePublisher : IMessagePublisher
{
    private readonly IProducer<string, string> _producer;

    public async Task PublishAsync(
        OutboxMessage message, 
        CancellationToken cancellationToken = default)
    {
        await _producer.ProduceAsync(
            topic: message.MessageType,
            new Message<string, string>
            {
                Key = message.OrderingKey,
                Value = message.Payload,
                Headers = new Headers
                {
                    { "MessageId", Encoding.UTF8.GetBytes(message.MessageId) },
                    { "CorrelationId", Encoding.UTF8.GetBytes(message.CorrelationId ?? "") }
                }
            },
            cancellationToken);
    }
}
```

## Safety and Limitations

### Duplicate Messages

**Reality:** Messages can be published more than once after crashes or network issues.

**Your responsibility:** Make your message handlers idempotent. Use the inbox pattern to track processed message IDs.

**Example of unsafe handler:**
```csharp
// ❌ UNSAFE - can charge customer twice
async Task ProcessPayment(PaymentMessage msg)
{
    await _paymentGateway.ChargeAsync(msg.Amount);
}
```

**Example of safe handler:**
```csharp
// ✅ SAFE - deduplicated
async Task ProcessPayment(PaymentMessage msg)
{
    if (!await _inboxStore.TryReserveAsync(msg.MessageId, ...))
        return; // Duplicate, skip
        
    await _paymentGateway.ChargeAsync(msg.Amount);
    await _inboxStore.MarkCompletedAsync(msg.MessageId, ...);
}
```

### Message Ordering

**Single key:** Messages with the same `OrderingKey` are processed in sequence by a single dispatcher instance.

**Different keys:** No ordering guarantee across different ordering keys.

**Partition assignment:** Order is preserved only if your message broker also preserves order (e.g., Kafka partition assignment).

### Security Considerations

**Payload size limits:** Enforced to prevent denial-of-service via large messages.

**SQL injection:** All queries use parameterized SQL. Never concatenate user input into raw SQL.

**Secrets in messages:** Never put credentials or sensitive keys in message payloads. Use secure vaults and reference them by ID.

### Recovery Scenarios

**Dispatcher crash before publish:**
- Message remains in `Pending` state
- Next dispatcher poll will claim and publish it
- No data loss

**Dispatcher crash after publish:**
- Message stays in `Publishing` state
- After `ClaimDurationSeconds`, lease expires
- Another dispatcher republishes it
- **Result:** Duplicate message (consumer must handle)

**Database failure during commit:**
- Transaction rolls back
- Neither business data nor message are saved
- No inconsistency

## Sample App

**Path:** `samples/TransactionalMessaging.Sample`

**Prerequisites:**
- .NET 8.0 SDK
- SQL Server LocalDB (included with Visual Studio)
- Database schema applied (run `src/TransactionalMessaging.SqlServer/Schema/CreateTables.sql`)

**Run the sample:**
```bash
cd samples/TransactionalMessaging.Sample
dotnet run
```

**What it demonstrates:**
1. Transactional outbox write (business transaction + message)
2. Background dispatcher publishing messages
3. Inbox deduplication (second attempt rejected)

**Expected output:**
```
=== TransactionalMessaging Sample ===

Prerequisites:
  - SQL Server LocalDB installed
  - Run schema scripts from src/TransactionalMessaging.SqlServer/Schema/

[Setup] Database 'TransactionalMessagingDemo' ready

=== Demonstrating Outbox Pattern ===

[Business] Processing order...
[Outbox] Message 3fa23c8d-... added to outbox
[Business] Order committed successfully

[Host] Starting background services...
[Publisher] Publishing message 3fa23c8d-... to topic 'orders.created'

=== Demonstrating Inbox Deduplication ===

[Inbox] First attempt: Message 7b91fa42-... reserved successfully
[Business] Processing incoming message...
[Inbox] First attempt: Message marked as completed
[Inbox] Second attempt: Duplicate message 7b91fa42-... rejected ✓

[Host] Press Ctrl+C to stop...
```

## Testing & Compatibility

### Running Tests

**Unit tests:**
```bash
dotnet test tests/TransactionalMessaging.Core.Tests
```

**Integration tests (require databases):**
```bash
# SQL Server (requires LocalDB)
dotnet test tests/TransactionalMessaging.SqlServer.IntegrationTests

# PostgreSQL (requires PostgreSQL server)
dotnet test tests/TransactionalMessaging.PostgreSql.IntegrationTests
```

**All tests:**
```bash
dotnet test
```

Integration tests skip automatically if the database is not available, with clear prerequisite messages.

### Framework Compatibility

- **.NET 8.0** — Fully supported
- **.NET 10.0** — Fully supported

### Database Compatibility

- **SQL Server 2016+** — Tested with LocalDB and SQL Server 2019/2022
- **PostgreSQL 11+** — Tested with PostgreSQL 14/15

## Versioning & License

**Version:** 1.0.0 (Stage 0 / V1 implementation)

**License:** Not specified. Contact repository owner for licensing terms.

**Project status:** Active development. API is subject to change before stable 1.0 release.

## Contributing

This is a private project. Contact the repository owner for contribution guidelines.

---

**Note:** This library provides infrastructure for reliable message publishing. It does not replace proper message broker selection, monitoring, or operational practices. Always test failure scenarios in your specific environment before production use.
