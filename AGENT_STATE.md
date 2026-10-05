# AGENT_STATE.md

> Compact authoritative resume checkpoint. Keep roughly 100–180 lines maximum.
> Historical execution detail belongs in ignored `agent_logs/`.

## Repository

- Project: TransactionalMessaging
- Repository: d:\freelance\sameer\i-gaming\TransactionalMessaging
- Current branch: master
- Base branch: main (per git status system-reminder)
- Last completed commit/checkpoint: None (no commits yet)
- Working tree status: Clean except agent-pack files

## Current phase/task

- Phase: Stage 0 / V1 implementation
- Active task: TM-01
- Task status: IN_PROGRESS
- Risk: HIGH
- Exact resume point: Repository initialization complete, starting core contract implementation

## Active authority boundary

- READ scope: `SPEC.md`, existing src/TransactionalMessaging.Core, src/TransactionalMessaging.SqlServer, src/TransactionalMessaging.PostgreSql, tests (storage/contract coverage)
- WRITE scope: src/TransactionalMessaging.Core/**, src/TransactionalMessaging.SqlServer/**, src/TransactionalMessaging.PostgreSql/**, tests/** (storage/contract coverage only)
- PROTECTED/inherited protected areas: AGENT_GUARDRAILS.md defaults, unrelated components, external systems, production/live config, secrets, other Stage-0 libraries
- External side effects: NONE
- Production authority: NONE

## Completed tasks

- [x] TM-01 partial: Core contracts implemented (OutboxMessage, InboxMessage, ClaimResult, states, enums)
- [x] TM-01 partial: Provider abstractions defined (IOutboxStore, IInboxStore, IMessagePublisher, IMessageSerializer)
- [x] TM-01 partial: SQL Server provider implemented with READPAST/UPDLOCK claim semantics
- [x] TM-01 partial: PostgreSQL provider implemented with FOR UPDATE SKIP LOCKED claim semantics
- [x] TM-01 partial: Message validation with SPEC.md size limits (256KB/1MB)
- [x] TM-01 partial: JSON serialization default
- [x] TM-01 partial: Retry delay calculator with exponential backoff + jitter
- [x] TM-01 partial: Fencing via ClaimToken in all state mutations
- [x] TM-01 partial: DI extensions for all packages
- [x] TM-01 partial: Core unit tests (19 passing)
- [x] TM-01 partial: SQL Server integration tests written (5 skipped, environment-dependent)

## Active-task changes

- Files changed: None (fresh implementation)
- New files:
  - Solution and project structure (Core, SqlServer, PostgreSql, Hosting, tests)
  - Core contracts: OutboxMessage, InboxMessage, ClaimResult, PublicationResult, enums
  - Core abstractions: IOutboxStore, IInboxStore, IMessagePublisher, IMessageSerializer
  - Core options: MessageOptions, RetryOptions, DispatcherOptions
  - Core implementation: JsonMessageSerializer, MessageValidator, RetryDelayCalculator
  - SqlServer provider: SqlServerOutboxStore, SqlServerInboxStore with SQL schemas
  - PostgreSql provider: PostgreSqlOutboxStore, PostgreSqlInboxStore with SQL schemas
  - DI extensions for all packages
- Deleted files: Template Class1.cs files
- Contract/schema changes: Created outbox and inbox schemas for both SQL Server and PostgreSQL
- Dependency changes:
  - Microsoft.Data.SqlClient 7.1.1 (SqlServer)
  - Npgsql 10.0.3 (PostgreSql)
  - Microsoft.Extensions.DependencyInjection.Abstractions 10.0.12 (all)

## Validation completed

- Build/compile: PASS — all projects build with 0 warnings, 0 errors
- Task-specific tests:
  - Core unit tests: PASS — 19/19 tests passing (validation, serialization, retry)
  - SQL Server integration: BLOCKED — 5 tests written but skipped (LocalDB unavailable, SC-001)
  - PostgreSQL integration: NOT_STARTED
  - Provider contract coverage: PARTIAL
- Targeted regression: NOT_STARTED
- Broader regression: NOT_STARTED
- Independent review (if required): REQUIRED_PENDING — HIGH-risk task
- Other checks: None yet

## Material evidence and assumptions

### VERIFIED

- Git initialized on master branch — Evidence: git status, git branch output
- .NET 10.0.101 SDK available — Evidence: dotnet --version
- Solution and all projects build successfully — Evidence: dotnet build output (0 warnings, 0 errors)
- Core contracts implement SPEC.md section 7 outbox record schema — Evidence: OutboxMessage.cs fields match spec
- Provider stores use database-specific claim semantics — Evidence: SQL Server READPAST/UPDLOCK, PostgreSQL FOR UPDATE SKIP LOCKED
- Fencing via ClaimToken implemented in all state mutations — Evidence: MarkPublished/ScheduleRetry/MoveToDeadLetter all check claimToken
- Serialization defaults to UTF-8 JSON — Evidence: JsonMessageSerializer.ContentType
- Message size limits follow SPEC.md section 10 — Evidence: MessageOptions defaults 256KB/1MB
- .gitignore includes agent_logs/ — Evidence: Write tool success

### ASSUMPTION

- No test databases available yet for integration testing — Impact: Will implement tests but may need to mark BLOCKED if databases unavailable — Verification: Attempt to run integration tests with local disposable databases

### DECISION

- Implement provider-specific SQL rather than attempting universal EF queries — Rationale: SPEC.md section 12 and 26 explicitly state correctness over portability — Authority: SPEC.md section 12, 26, 27
- Use TryAdd for service registration to enable idempotent calls — Rationale: Prevents duplicate service registration — Authority: October 2026 refinement DI requirements

## Open blockers

- SC-001: SQL Server LocalDB not available for integration tests (HIGH-risk contract validation blocked). Can continue with PostgreSQL tests and document SQL Server as environment-dependent.

## Scope-change references

- SC-001: SQL Server LocalDB unavailable (DISCOVERED)

## Ownership/conflict notes

- None / shared-file ownership or parallel-task coordination notes.

## Next approved action

- Write unit tests for core validation and serialization
- Implement first provider integration test (transaction atomicity test)
- Begin dispatcher implementation in Hosting package

## Last update

- Session: 2026-10-05 agent-01-core-storage — completed core contracts and provider stores
