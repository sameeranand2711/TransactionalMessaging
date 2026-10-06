# TransactionalMessaging V2 Development Tasks

## Task Status Legend
- **NOT_STARTED**: Task not yet begun
- **IN_PROGRESS**: Task actively being worked on
- **BLOCKED**: Task blocked by dependency or external issue
- **READY_FOR_REVIEW**: Implementation complete, awaiting review
- **COMPLETE**: Task finished and merged

## Authority Legend
- **READ**: Files/areas that may be read for context
- **WRITE**: Files/areas that may be modified
- **PROTECTED**: Files that must not be modified without explicit approval

---

## Phase 1: Observability & Diagnostics (v1.1.0)

### TM-V2-01: Structured Logging & Correlation
**Status:** NOT_STARTED  
**Priority:** HIGH  
**Risk:** MEDIUM  
**Assigned:** Unassigned  
**Estimated Effort:** 3-4 days

**Objective:**
Add comprehensive structured logging with correlation ID propagation throughout the message lifecycle (business transaction → outbox → publish → inbox).

**Scope:**
- Add correlation ID support to `OutboxMessage` and `InboxMessage`
- Implement structured logging in all core operations using `ILogger<T>`
- Create log scopes with message context (MessageId, MessageType, WorkerId, ClaimToken)
- Define diagnostic events: MessageWritten, MessageClaimed, MessagePublished, MessageFailed, MessageCompleted
- Add performance logging: operation duration, payload size
- Ensure no sensitive data (payload contents) logged by default

**Authority:**
- **READ**: All `src/TransactionalMessaging.Core/**`, `src/TransactionalMessaging.SqlServer/**`, `src/TransactionalMessaging.PostgreSql/**`, `src/TransactionalMessaging.Hosting/**`
- **WRITE**: 
  - `src/TransactionalMessaging.Core/Models/OutboxMessage.cs` (add CorrelationId property)
  - `src/TransactionalMessaging.Core/Models/InboxMessage.cs` (add CorrelationId property)
  - `src/TransactionalMessaging.Core/Logging/` (new directory for logging extensions)
  - `src/TransactionalMessaging.SqlServer/SqlServerOutboxStore.cs` (add logging)
  - `src/TransactionalMessaging.SqlServer/SqlServerInboxStore.cs` (add logging)
  - `src/TransactionalMessaging.PostgreSql/PostgreSqlOutboxStore.cs` (add logging)
  - `src/TransactionalMessaging.PostgreSql/PostgreSqlInboxStore.cs` (add logging)
  - `src/TransactionalMessaging.Hosting/Services/OutboxDispatcherService.cs` (add logging)
  - `src/TransactionalMessaging.Hosting/Services/CleanupService.cs` (add logging)
  - `schemas/sqlserver-outbox-schema.sql` (add CorrelationId column)
  - `schemas/sqlserver-inbox-schema.sql` (add CorrelationId column)
  - `schemas/postgresql-outbox-schema.sql` (add CorrelationId column)
  - `schemas/postgresql-inbox-schema.sql` (add CorrelationId column)
- **PROTECTED**: All other files, especially contracts (`IOutboxStore`, `IInboxStore`, `IMessagePublisher`)

**Definition of Done:**
- [ ] CorrelationId property added to OutboxMessage and InboxMessage
- [ ] Database schemas updated with CorrelationId column (nullable for backward compatibility)
- [ ] Structured logging added to all store operations (Write, Claim, MarkPublished, etc.)
- [ ] Structured logging added to dispatcher and cleanup services
- [ ] Log scopes include message context (MessageId, MessageType, WorkerId)
- [ ] Performance metrics logged (operation duration)
- [ ] Unit tests verify logging behavior
- [ ] Sample application demonstrates correlation ID flow
- [ ] No payload contents logged (privacy/security)

**Blocked By:** None

**Dependencies:** None

---

### TM-V2-02: Metrics & Health Checks
**Status:** NOT_STARTED  
**Priority:** HIGH  
**Risk:** MEDIUM  
**Assigned:** Unassigned  
**Estimated Effort:** 4-5 days

**Objective:**
Add production-ready health checks and metrics using ASP.NET Core health checks and System.Diagnostics.Metrics.

**Scope:**
- Create new package: `TransactionalMessaging.HealthChecks`
- Implement `OutboxStoreHealthCheck` (database connectivity, table schema validation)
- Implement `InboxStoreHealthCheck` (database connectivity, table schema validation)
- Implement `DispatcherHealthCheck` (dispatcher running, processing messages)
- Add metrics using `System.Diagnostics.Metrics.Meter`
- Metrics: outbox queue depth, inbox reservation count, publish success/failure rates
- Metrics: message age (time in Pending state), claim contention
- Optional OpenTelemetry integration
- Sample ASP.NET Core app with `/health` and `/metrics` endpoints

**Authority:**
- **READ**: All `src/TransactionalMessaging.Core/**`, `src/TransactionalMessaging.Hosting/**`
- **WRITE**:
  - `src/TransactionalMessaging.HealthChecks/` (new project)
  - `src/TransactionalMessaging.HealthChecks/OutboxStoreHealthCheck.cs` (new)
  - `src/TransactionalMessaging.HealthChecks/InboxStoreHealthCheck.cs` (new)
  - `src/TransactionalMessaging.HealthChecks/DispatcherHealthCheck.cs` (new)
  - `src/TransactionalMessaging.HealthChecks/ServiceCollectionExtensions.cs` (new)
  - `src/TransactionalMessaging.Core/Metrics/` (new directory)
  - `src/TransactionalMessaging.Core/Metrics/TransactionalMessagingMeter.cs` (new)
  - `src/TransactionalMessaging.Hosting/Services/OutboxDispatcherService.cs` (add metrics)
  - `samples/TransactionalMessaging.HealthChecksSample/` (new sample project)
- **PROTECTED**: Core contracts, existing store implementations

**Definition of Done:**
- [ ] `TransactionalMessaging.HealthChecks` package created
- [ ] Health checks implemented for outbox/inbox stores
- [ ] Health checks validate database connectivity and schema
- [ ] Dispatcher health check reports running/stopped state
- [ ] Metrics exposed via `System.Diagnostics.Metrics.Meter`
- [ ] Metrics: queue depth, reservation count, publish success/failure
- [ ] Metrics: message age, claim duration
- [ ] Sample app demonstrates `/health` endpoint with JSON response
- [ ] Sample app demonstrates metrics collection
- [ ] Unit tests for health check logic
- [ ] Integration tests verify health checks with real database
- [ ] Documentation: metrics catalog and health check configuration

**Blocked By:** None (but benefits from TM-V2-01 logging)

**Dependencies:**
- TM-V2-01 (optional, for better diagnostics)

---

### TM-V2-03: Diagnostic Commands & Tooling
**Status:** NOT_STARTED  
**Priority:** MEDIUM  
**Risk:** LOW  
**Assigned:** Unassigned  
**Estimated Effort:** 5-6 days

**Objective:**
Create CLI tool for diagnosing production issues and managing messages.

**Scope:**
- Create new global tool: `TransactionalMessaging.Cli`
- Commands: `status`, `inspect`, `retry`, `dead-letter`, `cleanup --dry-run`
- Support both SQL Server and PostgreSQL connection strings
- Output formats: JSON and human-readable table
- Connection string from config file or command line argument
- Safe operations: read-only by default, write operations require confirmation

**Authority:**
- **READ**: All `src/**`
- **WRITE**:
  - `tools/TransactionalMessaging.Cli/` (new project)
  - `tools/TransactionalMessaging.Cli/Program.cs` (new)
  - `tools/TransactionalMessaging.Cli/Commands/` (new directory)
  - `tools/TransactionalMessaging.Cli/Commands/StatusCommand.cs` (new)
  - `tools/TransactionalMessaging.Cli/Commands/InspectCommand.cs` (new)
  - `tools/TransactionalMessaging.Cli/Commands/RetryCommand.cs` (new)
  - `tools/TransactionalMessaging.Cli/Commands/DeadLetterCommand.cs` (new)
  - `tools/TransactionalMessaging.Cli/Commands/CleanupCommand.cs` (new)
  - `tools/TransactionalMessaging.Cli/Formatters/` (JSON and table formatters)
  - `docs/CLI_GUIDE.md` (new documentation)
- **PROTECTED**: Core library code, existing samples

**Definition of Done:**
- [ ] CLI tool project created and packaged as global tool
- [ ] `status` command shows queue depths, oldest message, worker status
- [ ] `inspect` command shows message details, retry count, history
- [ ] `retry` command reschedules failed message to Pending
- [ ] `dead-letter` command moves message to DeadLetter state
- [ ] `cleanup --dry-run` previews cleanup operation
- [ ] Commands work with SQL Server and PostgreSQL
- [ ] Output formats: JSON and human-readable table
- [ ] Connection string from config or `--connection-string` argument
- [ ] Write operations require `--confirm` flag
- [ ] Unit tests for command logic
- [ ] Integration tests against real database
- [ ] CLI guide documentation with examples

**Blocked By:** None

**Dependencies:**
- TM-V2-01 (optional, for correlation ID inspection)
- TM-V2-02 (optional, for metrics in status command)

---

## Phase 2: Resilience & Recovery (v1.2.0)

### TM-V2-04: Circuit Breaker & Backpressure
**Status:** NOT_STARTED  
**Priority:** HIGH  
**Risk:** HIGH  
**Assigned:** Unassigned  
**Estimated Effort:** 4-5 days

**Objective:**
Implement circuit breaker pattern around message publisher to prevent cascading failures when broker unavailable.

**Scope:**
- Circuit breaker decorator wrapping `IMessagePublisher`
- Circuit states: Closed (normal), Open (broker down), Half-Open (testing recovery)
- Configuration: failure threshold, open duration, half-open test interval
- Backpressure: dispatcher pauses claiming when circuit open
- Metrics: circuit state changes, open duration
- Graceful degradation: log warnings, don't crash

**Authority:**
- **READ**: `src/TransactionalMessaging.Core/**`, `src/TransactionalMessaging.Hosting/**`
- **WRITE**:
  - `src/TransactionalMessaging.Core/Publishers/CircuitBreakerPublisherDecorator.cs` (new)
  - `src/TransactionalMessaging.Core/Publishers/CircuitBreakerOptions.cs` (new)
  - `src/TransactionalMessaging.Core/Publishers/CircuitState.cs` (new enum)
  - `src/TransactionalMessaging.Core/ServiceCollectionExtensions.cs` (add `.WithCircuitBreaker()`)
  - `src/TransactionalMessaging.Hosting/Services/OutboxDispatcherService.cs` (pause on circuit open)
  - `tests/TransactionalMessaging.Core.Tests/Publishers/CircuitBreakerTests.cs` (new)
- **PROTECTED**: `IMessagePublisher` interface (cannot change signature)

**Definition of Done:**
- [ ] Circuit breaker decorator implemented
- [ ] Circuit opens after configured failure threshold
- [ ] Circuit closes after success in half-open state
- [ ] Dispatcher pauses claiming when circuit open
- [ ] Dispatcher resumes when circuit closes
- [ ] No message loss during circuit transitions
- [ ] Metrics track circuit state changes
- [ ] Unit tests for circuit state transitions
- [ ] Integration tests with simulated broker failures
- [ ] Documentation: circuit breaker configuration guide

**Blocked By:** Phase 1 (v1.1.0) complete

**Dependencies:**
- TM-V2-02 (metrics for circuit state tracking)

---

### TM-V2-05: Enhanced Poison Message Handling
**Status:** NOT_STARTED  
**Priority:** MEDIUM  
**Risk:** MEDIUM  
**Assigned:** Unassigned  
**Estimated Effort:** 3-4 days

**Objective:**
Intelligent handling of messages that consistently fail, with dead letter quarantine and replay capability.

**Scope:**
- Poison message detection: max retry count exceeded → dead letter
- Dead letter reason tracking: MaxRetriesExceeded, SerializationFailure, PublisherRejected, Manual
- Optional `IDeadLetterHandler` for custom logic
- Dead letter metadata: error message, first failure time, retry history
- CLI command: `replay-dead-letter` to reset to Pending
- Critical alert logging when message dead-lettered

**Authority:**
- **READ**: All `src/**`
- **WRITE**:
  - `src/TransactionalMessaging.Core/Handlers/IDeadLetterHandler.cs` (new interface)
  - `src/TransactionalMessaging.Core/Models/DeadLetterReason.cs` (new enum)
  - `src/TransactionalMessaging.Core/Models/OutboxMessage.cs` (add DeadLetterReason, DeadLetterMetadata)
  - `src/TransactionalMessaging.Hosting/Services/OutboxDispatcherService.cs` (dead letter detection)
  - `schemas/sqlserver-outbox-schema.sql` (add DeadLetterReason, DeadLetterMetadata columns)
  - `schemas/postgresql-outbox-schema.sql` (add DeadLetterReason, DeadLetterMetadata columns)
  - `tools/TransactionalMessaging.Cli/Commands/ReplayDeadLetterCommand.cs` (new)
- **PROTECTED**: Core contracts (minimize changes)

**Definition of Done:**
- [ ] Dead letter reason enum defined
- [ ] Dead letter metadata captured (error, timestamps)
- [ ] Dispatcher moves message to dead letter after max retries
- [ ] `IDeadLetterHandler` interface for custom handling
- [ ] Critical log alert when message dead-lettered
- [ ] CLI `replay-dead-letter` command resets to Pending
- [ ] Unit tests for poison message detection
- [ ] Integration tests verify dead letter flow
- [ ] Documentation: poison message handling guide

**Blocked By:** Phase 1 (v1.1.0) complete

**Dependencies:**
- TM-V2-01 (logging for alerts)
- TM-V2-03 (CLI for replay command)

---

### TM-V2-06: Disaster Recovery & Data Retention
**Status:** NOT_STARTED  
**Priority:** MEDIUM  
**Risk:** HIGH  
**Assigned:** Unassigned  
**Estimated Effort:** 5-6 days

**Objective:**
Provide tools and procedures for disaster recovery, archival, and data retention.

**Scope:**
- Disaster recovery playbook documentation
- Archive tables for Published/Completed messages
- Archival service: move old messages to archive (opt-in)
- Purge utility: delete archived messages beyond retention
- Validation queries: detect gaps, orphaned claims
- Backup/restore guidance

**Authority:**
- **READ**: All `src/**`, `schemas/**`
- **WRITE**:
  - `schemas/sqlserver-archive-schema.sql` (new)
  - `schemas/postgresql-archive-schema.sql` (new)
  - `src/TransactionalMessaging.Core/Services/OutboxArchivalService.cs` (new)
  - `src/TransactionalMessaging.Core/Services/InboxArchivalService.cs` (new)
  - `src/TransactionalMessaging.Core/Options/ArchivalOptions.cs` (new)
  - `docs/DISASTER_RECOVERY.md` (new)
  - `docs/DATA_RETENTION.md` (new)
  - `tools/TransactionalMessaging.Cli/Commands/ArchiveCommand.cs` (new)
  - `tools/TransactionalMessaging.Cli/Commands/PurgeCommand.cs` (new)
- **PROTECTED**: Main outbox/inbox tables (archival only, no destructive changes)

**Definition of Done:**
- [ ] Archive table schemas created for SQL Server and PostgreSQL
- [ ] Archival service moves Published messages to archive
- [ ] Archival respects retention period (default 90 days)
- [ ] Purge utility deletes archived messages beyond retention
- [ ] Validation queries detect gaps in message sequence
- [ ] Disaster recovery playbook documented
- [ ] CLI commands: `archive`, `purge` with `--dry-run`
- [ ] Unit tests for archival logic
- [ ] Integration tests verify archival and purge
- [ ] Backup/restore tested against real scenario

**Blocked By:** Phase 1 (v1.1.0) complete

**Dependencies:**
- TM-V2-03 (CLI for archive/purge commands)

---

## Phase 3: Performance & Scalability (v1.3.0)

_(Tasks TM-V2-07 through TM-V2-09 to be detailed when Phase 2 nears completion)_

## Phase 4: Ecosystem Integration (v1.4.0)

_(Tasks TM-V2-10 through TM-V2-12 to be detailed when Phase 2 nears completion)_

## Phase 5: Developer Experience (v1.5.0)

_(Tasks TM-V2-13 through TM-V2-15 to be detailed when Phase 3 nears completion)_

---

## Notes

- All tasks follow the same Definition of Done standards from V1
- Each task must include unit tests and integration tests
- Each task must update relevant documentation
- Schema changes must be backward compatible where possible
- Breaking changes require major version bump and migration guide
- All code must follow `DEVELOPMENT_STANDARDS.md`
- All work must respect `AGENT_GUARDRAILS.md` and `AGENT_RULES.md`

---

*Last Updated: 2026-10-06*
