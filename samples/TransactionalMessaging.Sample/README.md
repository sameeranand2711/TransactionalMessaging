# TransactionalMessaging Sample

This sample demonstrates the complete TransactionalMessaging pattern with SQL Server LocalDB.

## Features Demonstrated

1. **Outbox Pattern**: Messages are written to the outbox in the same transaction as business operations
2. **Background Dispatcher**: Polls and publishes pending messages
3. **Inbox Deduplication**: Prevents duplicate message processing
4. **Fluent DI Registration**: Using the builder pattern for clean setup

## Prerequisites

- .NET 8.0 SDK
- SQL Server LocalDB (installed with Visual Studio)
- Database schema applied (run scripts from `src/TransactionalMessaging.SqlServer/Schema/`)

## Running the Sample

```bash
# Navigate to sample directory
cd samples/TransactionalMessaging.Sample

# Run the sample
dotnet run
```

## What It Does

1. Creates a test database (`TransactionalMessagingDemo`) if it doesn't exist
2. Adds a message to the outbox within a business transaction
3. Background dispatcher picks up and publishes the message
4. Demonstrates inbox deduplication by attempting to process the same message twice

## DI Configuration

The sample shows the recommended fluent registration:

```csharp
builder.Services
    .AddTransactionalMessaging()
    .UseSqlServer(connectionString)
    .AddDispatcher()
    .AddInboxCleanup();

// Register your message publisher
builder.Services.AddSingleton<IMessagePublisher, YourPublisher>();
```

## Expected Output

```
=== TransactionalMessaging Sample ===

Prerequisites:
  - SQL Server LocalDB installed
  - Run schema scripts from src/TransactionalMessaging.SqlServer/Schema/

[Setup] Database 'TransactionalMessagingDemo' ready

=== Demonstrating Outbox Pattern ===

[Business] Processing order...
[Outbox] Message <guid> added to outbox
[Business] Order committed successfully

[Host] Starting background services...
[Publisher] Publishing message <guid> to topic 'orders.created'

=== Demonstrating Inbox Deduplication ===

[Inbox] First attempt: Message <guid> reserved successfully
[Business] Processing incoming message...
[Inbox] First attempt: Message marked as completed
[Inbox] Second attempt: Duplicate message <guid> rejected ✓

[Host] Press Ctrl+C to stop...
```
