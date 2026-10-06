# TransactionalMessaging V2 Roadmap

## Overview

V1 (v1.0.0-rc.1) provides the core transactional outbox/inbox pattern with SQL Server and PostgreSQL support, basic dispatcher, and cleanup services. V2 will focus on production-readiness enhancements, observability, resilience, and ecosystem integration.

## V2 Goals

1. **Production Observability** - Comprehensive metrics, logging, and health checks
2. **Resilience & Recovery** - Circuit breakers, poison message handling, disaster recovery
3. **Performance Optimization** - Batching improvements, connection pooling, indexing guidance
4. **Ecosystem Integration** - Real message broker adapters (Kafka, RabbitMQ, Azure Service Bus)
5. **Developer Experience** - Better diagnostics, migration tools, performance testing utilities

---

## Phase 1: Observability & Diagnostics (v1.1.0)

### TM-V2-01: Structured Logging & Correlation
**Priority:** HIGH | **Risk:** MEDIUM

**Goal:** Add structured logging with correlation IDs throughout the message lifecycle.

**Features:**
- Correlation ID propagation (business transaction → outbox → publish → inbox)
- Structured logging using `ILogger<T>` with semantic log levels
- Log scopes for message context (MessageId, MessageType, WorkerId, ClaimToken)
- Diagnostic events: MessageWritten, MessageClaimed, MessagePublished, MessageFailed, etc.
- Performance logging: claim duration, publish duration, payload size

**Acceptance:**
- Every major operation logs with correlation ID
- Log levels are appropriate (Debug/Info/Warning/Error)
- Logs are filterable by MessageId, WorkerId, MessageType
- No sensitive data (payload contents) in logs by default

---

### TM-V2-02: Metrics & Health Checks
**Priority:** HIGH | **Risk:** MEDIUM

**Goal:** Expose production-ready metrics and health checks.

**Features:**
- ASP.NET Core health checks for outbox/inbox stores
- Health check: database connectivity, table schema validation
- Health check: dispatcher running and processing messages
- Metrics: outbox queue depth, inbox reservation rate, publish success/failure rates
- Metrics: message age (time in Pending state), claim contention
- Integration with standard .NET metrics (`System.Diagnostics.Metrics`)
- Optional OpenTelemetry exporter support

**Deliverables:**
- `TransactionalMessaging.HealthChecks` package
- `IHealthCheck` implementations: `OutboxStoreHealthCheck`, `InboxStoreHealthCheck`, `DispatcherHealthCheck`
- Metrics exposed via `Meter` with standard naming conventions
- Sample: ASP.NET Core app with `/health` and `/metrics` endpoints

**Acceptance:**
- Health checks pass when stores are accessible
- Health checks fail gracefully when database unavailable
- Metrics accurately reflect message processing statistics
- Sample demonstrates health endpoint returning JSON status

---

### TM-V2-03: Diagnostic Commands & Tooling
**Priority:** MEDIUM | **Risk:** LOW

**Goal:** CLI tooling for diagnosing production issues.

**Features:**
- CLI tool: `dotnet transactional-messaging`
- Commands:
  - `status` - Show outbox/inbox queue depths, oldest message, worker status
  - `inspect <messageId>` - Show message details, history, retry count
  - `retry <messageId>` - Manually reschedule a failed message
  - `dead-letter <messageId>` - Manually move to dead letter
  - `cleanup --dry-run` - Preview cleanup operation
- Connection string from config or command line
- Output: JSON or human-readable table format

**Deliverables:**
- `TransactionalMessaging.Cli` tool project
- Documentation: troubleshooting guide using CLI commands

**Acceptance:**
- CLI connects to live database and shows accurate statistics
- Commands work against both SQL Server and PostgreSQL
- `--dry-run` mode makes no changes
- JSON output is machine-parseable

---

## Phase 2: Resilience & Recovery (v1.2.0)

### TM-V2-04: Circuit Breaker & Backpressure
**Priority:** HIGH | **Risk:** HIGH

**Goal:** Prevent cascading failures when broker is unavailable.

**Features:**
- Circuit breaker around `IMessagePublisher.PublishAsync`
- Circuit states: Closed (normal), Open (broker down), Half-Open (testing recovery)
- Circuit breaker configuration: failure threshold, timeout, retry interval
- Backpressure: dispatcher pauses when circuit open, resumes when closed
- Metrics: circuit breaker state changes, open duration
- Graceful degradation: log warnings, don't crash dispatcher

**Deliverables:**
- `CircuitBreakerPublisherDecorator` wrapping `IMessagePublisher`
- `CircuitBreakerOptions`: `FailureThreshold`, `OpenDuration`, `HalfOpenTestInterval`
- DI extension: `.WithCircuitBreaker()` on publisher registration
- Tests: circuit opens after N failures, closes after success in half-open

**Acceptance:**
- Circuit opens after configured failure threshold
- Dispatcher pauses claiming new messages when circuit open
- Circuit transitions to half-open and tests recovery
- No message loss during circuit open/close transitions

---

### TM-V2-05: Enhanced Poison Message Handling
**Priority:** MEDIUM | **Risk:** MEDIUM

**Goal:** Intelligent handling of messages that consistently fail.

**Features:**
- Poison message detection: message exceeds max retry count → dead letter
- Dead letter with reason: serialization failure, timeout, publisher rejection
- Dead letter quarantine: separate table or state for investigation
- Optional dead letter callback: `IDeadLetterHandler` for custom logic
- Admin capability: replay dead-lettered message after fix
- Alerting: log critical alert when message dead-lettered

**Deliverables:**
- `IDeadLetterHandler` interface for custom handling
- Dead letter reason enum: `MaxRetriesExceeded`, `SerializationFailure`, `PublisherRejected`, `Manual`
- Dead letter metadata: original error, first failure time, retry history
- CLI command: `replay-dead-letter <messageId>` → resets to Pending

**Acceptance:**
- Message moves to dead letter after max retries
- Dead letter reason is accurately recorded
- Dead-lettered messages do not block dispatcher
- Replay command successfully reprocesses message

---

### TM-V2-06: Disaster Recovery & Data Retention
**Priority:** MEDIUM | **Risk:** HIGH

**Goal:** Tools and procedures for disaster recovery scenarios.

**Features:**
- Backup/restore guidance for outbox/inbox tables
- Point-in-time recovery: replay messages from specific timestamp
- Archive strategy: move old Published/Completed messages to archive tables
- Archive retention: configurable retention for archive (default 90 days)
- Purge utility: safely delete archived messages beyond retention
- Recovery verification: checksum/count validation after restore

**Deliverables:**
- Documentation: disaster recovery playbook
- SQL scripts: archive table creation, archival stored procedures
- Archival service: `OutboxArchivalService`, `InboxArchivalService` (opt-in)
- Validation queries: detect gaps in message sequence, orphaned claims

**Acceptance:**
- Archive moves Published messages to archive table
- Archived messages are not claimed by dispatcher
- Purge deletes only archived messages beyond retention
- Recovery playbook tested against backup restore scenario

---

## Phase 3: Performance & Scalability (v1.3.0)

### TM-V2-07: Connection Pooling & Resource Management
**Priority:** HIGH | **Risk:** MEDIUM

**Goal:** Optimize database connection usage and resource efficiency.

**Features:**
- Connection pooling guidance for SQL Server and PostgreSQL
- Configurable connection pool size per worker
- Connection health checks: detect and recycle stale connections
- Lease renewal optimization: batch renewal for multiple messages
- Parallel publish with bounded concurrency (already exists, tune defaults)
- Memory profiling: ensure dispatcher doesn't leak resources

**Deliverables:**
- Documentation: connection pooling best practices
- `DispatcherOptions`: `MaxConnectionPoolSize`, `ConnectionIdleTimeout`
- Batch lease renewal: `RenewLeaseBatchAsync(claimTokens[])`
- Performance tests: measure throughput with various pool sizes

**Acceptance:**
- Dispatcher operates within connection pool limits
- No connection leaks under sustained load
- Batch lease renewal reduces database round trips
- Performance tests document throughput improvements

---

### TM-V2-08: Indexing & Query Optimization
**Priority:** MEDIUM | **Risk:** LOW

**Goal:** Ensure optimal database performance at scale.

**Features:**
- Index recommendations: covering indexes for claim queries
- Query plan analysis: document execution plans for ClaimBatch
- Partition guidance: table partitioning strategies for high-volume scenarios
- Archival automation: partition pruning for old Published data
- Statistics maintenance: guidance for index statistics updates

**Deliverables:**
- Documentation: database performance tuning guide
- SQL scripts: recommended additional indexes (optional)
- Query analysis: execution plans for claim/publish/cleanup operations
- Load test: 10,000 messages/second sustained throughput test

**Acceptance:**
- Claim query uses recommended indexes
- No table scans in claim/publish/cleanup queries
- Partition strategy documented for 1M+ messages
- Load test demonstrates scalability limits

---

### TM-V2-09: Bulk Operations & Batch Publishing
**Priority:** MEDIUM | **Risk:** MEDIUM

**Goal:** Efficiently handle bulk message scenarios.

**Features:**
- Bulk write: `IOutboxStore.WriteBatchAsync(messages[])`
- Transaction scope: write multiple business entities + messages in one transaction
- Batch publish optimization: publish multiple messages to broker in one call
- Publisher interface extension: `IMessagePublisher.PublishBatchAsync(messages[])`
- Batching strategy: adaptive batch size based on payload size
- Memory limits: prevent OOM with large batches

**Deliverables:**
- `IOutboxStore.WriteBatchAsync` method
- Batch publishing support in dispatcher
- Sample: bulk import scenario with 1000 messages in single transaction
- Performance comparison: bulk vs. individual writes

**Acceptance:**
- Bulk write commits all messages or none (transaction atomicity)
- Batch publish reduces broker round trips
- Memory usage stays bounded with large batches
- Performance tests show 5-10x improvement for bulk scenarios

---

## Phase 4: Ecosystem Integration (v1.4.0)

### TM-V2-10: Kafka Publisher Integration
**Priority:** HIGH | **Risk:** MEDIUM

**Goal:** Production-ready Kafka publisher implementation.

**Features:**
- `TransactionalMessaging.Kafka` package
- `KafkaMessagePublisher` implementing `IMessagePublisher`
- Kafka producer configuration: compression, acks, retries, idempotence
- Topic routing: message type → Kafka topic mapping
- Partition key support: route messages by OrderingKey
- Error handling: transient vs. permanent Kafka errors
- Health check: Kafka broker connectivity

**Deliverables:**
- `TransactionalMessaging.Kafka` package
- DI extension: `.UseKafkaPublisher(options)`
- Sample: Kafka producer with transactional outbox
- Integration tests: Kafka with Testcontainers

**Acceptance:**
- Messages publish to correct Kafka topics
- Kafka producer respects `at-least-once` guarantee
- Transient errors trigger retry, permanent errors dead-letter
- Health check detects broker unavailability

---

### TM-V2-11: RabbitMQ Publisher Integration
**Priority:** MEDIUM | **Risk:** MEDIUM

**Goal:** Production-ready RabbitMQ publisher implementation.

**Features:**
- `TransactionalMessaging.RabbitMQ` package
- `RabbitMqMessagePublisher` implementing `IMessagePublisher`
- RabbitMQ configuration: exchange, routing key, durability, confirm mode
- Exchange routing: message type → exchange + routing key mapping
- Publisher confirms: wait for broker acknowledgment
- Connection recovery: automatic reconnection on connection loss
- Health check: RabbitMQ connection status

**Deliverables:**
- `TransactionalMessaging.RabbitMQ` package
- DI extension: `.UseRabbitMqPublisher(options)`
- Sample: RabbitMQ producer with transactional outbox
- Integration tests: RabbitMQ with Testcontainers

**Acceptance:**
- Messages publish to correct exchanges with routing keys
- Publisher confirms ensure broker persistence
- Connection recovery handles transient network failures
- Health check detects broker unavailability

---

### TM-V2-12: Azure Service Bus Publisher Integration
**Priority:** MEDIUM | **Risk:** MEDIUM

**Goal:** Production-ready Azure Service Bus publisher implementation.

**Features:**
- `TransactionalMessaging.AzureServiceBus` package
- `AzureServiceBusMessagePublisher` implementing `IMessagePublisher`
- Service Bus configuration: queue/topic selection, session support
- Message properties: set Service Bus message properties, user properties
- Partition key: route messages by OrderingKey to partitioned entities
- Managed identity support: authenticate without connection strings
- Health check: Service Bus namespace connectivity

**Deliverables:**
- `TransactionalMessaging.AzureServiceBus` package
- DI extension: `.UseAzureServiceBusPublisher(options)`
- Sample: Azure Service Bus producer with transactional outbox
- Integration tests: Azure Service Bus emulator or Azurite

**Acceptance:**
- Messages publish to correct queues/topics
- Session support enables ordered message processing
- Managed identity authentication works in Azure environments
- Health check detects Service Bus unavailability

---

## Phase 5: Developer Experience (v1.5.0)

### TM-V2-13: Database Migrations & Schema Versioning
**Priority:** HIGH | **Risk:** LOW

**Goal:** Smooth database schema evolution and migrations.

**Features:**
- Schema versioning: track schema version in database
- Migration scripts: v1.0 → v1.1 → v1.2 etc.
- Migration tool: `dotnet transactional-messaging migrate`
- Migration verification: check current schema version, detect drift
- Rollback support: downgrade scripts for each version
- Zero-downtime migrations: additive changes first, then deprecation

**Deliverables:**
- Schema version table: `__TransactionalMessaging_SchemaVersion`
- Migration scripts: SQL Server and PostgreSQL for each version
- CLI command: `migrate up`, `migrate down`, `migrate status`
- Documentation: migration strategy and version history

**Acceptance:**
- Migration tool detects current schema version
- Migrations apply cleanly from any supported version to latest
- Rollback scripts successfully downgrade schema
- Zero-downtime migration strategy documented

---

### TM-V2-14: Testing Utilities & Test Helpers
**Priority:** MEDIUM | **Risk:** LOW

**Goal:** Make it easy for consumers to test outbox/inbox scenarios.

**Features:**
- `TransactionalMessaging.Testing` package
- In-memory test doubles: `InMemoryOutboxStore`, `InMemoryInboxStore`
- Test publisher: `TestMessagePublisher` that captures published messages
- Test builders: fluent API for creating test messages
- Fake clock: control time for testing lease expiration, retry delays
- Assertion helpers: `AssertPublished(messageId)`, `AssertNotPublished(messageId)`

**Deliverables:**
- `TransactionalMessaging.Testing` package
- In-memory implementations for all stores
- Test fixture: `TransactionalMessagingTestFixture` for integration tests
- Sample: unit tests using test helpers
- Documentation: testing guide

**Acceptance:**
- In-memory stores behave like real stores (atomic, fencing)
- Test publisher captures messages without side effects
- Fake clock enables time-based test scenarios
- Sample demonstrates testing business logic with outbox

---

### TM-V2-15: Performance Benchmarking Suite
**Priority:** LOW | **Risk:** LOW

**Goal:** Establish performance baselines and regression detection.

**Features:**
- BenchmarkDotNet suite for core operations
- Benchmarks: WriteAsync, ClaimBatch, MarkPublished, TryReserve
- Benchmarks: end-to-end (write → claim → publish → inbox)
- Benchmark scenarios: payload sizes (1KB, 10KB, 100KB, 256KB)
- Benchmark scenarios: concurrency levels (1, 4, 8, 16 workers)
- Comparison: SQL Server vs. PostgreSQL performance
- CI integration: fail build on significant regression

**Deliverables:**
- `benchmarks/TransactionalMessaging.Benchmarks` project
- Benchmark reports: baseline measurements for v1.0.0
- CI job: run benchmarks on every PR
- Documentation: performance characteristics and known limits

**Acceptance:**
- Benchmarks run reliably and produce consistent results
- Benchmarks cover key performance-critical paths
- CI detects regressions > 20% slower than baseline
- Documentation includes performance recommendations

---

## Dependencies & Sequencing

```
Phase 1: Observability (v1.1.0)
  TM-V2-01 → TM-V2-02 → TM-V2-03

Phase 2: Resilience (v1.2.0)
  Dependencies: Phase 1 complete
  TM-V2-04 → TM-V2-05 → TM-V2-06

Phase 3: Performance (v1.3.0)
  Dependencies: Phase 1, Phase 2 complete
  TM-V2-07 → TM-V2-08 → TM-V2-09

Phase 4: Ecosystem (v1.4.0)
  Dependencies: Phase 1, Phase 2 complete
  TM-V2-10, TM-V2-11, TM-V2-12 (parallel)

Phase 5: DX (v1.5.0)
  Dependencies: Phase 3 complete
  TM-V2-13 → TM-V2-14, TM-V2-15 (parallel)
```

## Version Timeline

- **v1.0.0** (GA): Merge current v1.0.0-rc.1 PRs
- **v1.1.0** (Q1): Observability & Diagnostics
- **v1.2.0** (Q2): Resilience & Recovery
- **v1.3.0** (Q3): Performance & Scalability
- **v1.4.0** (Q4): Ecosystem Integration
- **v1.5.0** (Q1 next year): Developer Experience
- **v2.0.0** (Q2 next year): Breaking changes if needed

## Success Metrics

- **Observability:** Every production issue diagnosable from logs/metrics
- **Resilience:** System recovers automatically from transient failures
- **Performance:** 10,000 messages/second sustained throughput
- **Ecosystem:** Support for 3 major message brokers
- **DX:** Zero-friction testing, clear migration paths

---

## Out of Scope for V2

- **Distributed transactions** (2PC/XA) - complexity outweighs benefit
- **Saga orchestration** - different pattern, separate library concern
- **Event sourcing** - specialized pattern, not general-purpose outbox
- **Multi-database transactions** - V1/V2 focus on single-database atomicity
- **Real-time streaming** - outbox pattern is async by design
- **UI/Dashboard** - focus on library, not monitoring tools
- **Custom database providers** - SQL Server and PostgreSQL only

---

*This roadmap is a living document and will be refined based on community feedback and production usage patterns.*
