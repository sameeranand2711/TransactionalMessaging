# SPEC.md — TransactionalMessaging V1

## Source precedence
This specification uses the Stage-0 V1 architecture below. The **October 2026 refinement** preceding it is the latest explicit user request, and therefore governs DI, single sample app and README if it differs from earlier wording. Existing code must be inspected; do not invent repo facts. Do not expand other libraries.


## October 2026 refinement — minimal DI + sample + consumer README

These explicit user-approved additions refine the above V1 design without expanding its library scope:

- **DI required:** provide conventional `IServiceCollection` extension methods in appropriate hosting/provider package(s) to register core services, the selected SQL Server or PostgreSQL store, background dispatcher only when explicitly opted in, and one configured publisher. Use existing public abstractions instead of proliferating builder factories. `TryAdd`/idempotent registration where appropriate; validate missing provider/publisher/options early. Resolve `Scoped` DB dependencies correctly in hosted work; cancellation and disposal must behave predictably. Unit-test default lifetimes, missing dependencies and duplicate registration behavior.
- **One small sample:** `samples/TransactionalMessaging.Sample` is a console/Generic Host app (no web UI or full microservice suite). Default demo publisher is a local in-process test/console transport, **not** a claim of durable broker delivery. A local SQL Server or PostgreSQL database demonstrates a committed business row plus outbox, dispatcher publication and inbox duplicate suppression with known message IDs. Keep a single obvious command/run path; never run against production. The default may require a local disposable DB, documented honestly. Do not create a new SQLite provider or broker framework merely to make the demo easier.
- **KafkaHighThroughput boundary:** existing architecture expects a Kafka integration example. Keep broker abstraction independent. If an existing KafkaHighThroughput producer API/package is VERIFIABLY present, demonstrate it as a small optional adapter in the same sample without requiring a broker for the default demo; otherwise record the missing optional integration in `SCOPE_CHANGES.md`, avoid invented APIs, and flag any outstanding original release criterion explicitly instead of claiming a complete Stage-0 release.
- **README:** update final project `README.md` only after DI and the small sample actually compile/run; use the mandatory README structure below.
- **No extra infrastructure:** no distributed transaction coordinator, no cloud deployment, no new iGaming domain projects, no new generic orchestration tooling.


## Final consumer README (mandatory, generated during implementation)

The repository `README.md` is a final **project deliverable**, not an agent-pack instruction file. Do not overwrite a useful pre-existing README until its structure is understood; update it in place. Do not prewrite hypothetical API examples.

After the actual DI API and console sample compile and run, update `README.md` with this navigable structure:

1. **Overview** — what the library does and does not do.
2. **Install** — correct NuGet/project references, frameworks and package selection.
3. **Quick start** — a minimal, verified DI registration snippet followed by the actual usage snippet.
4. **Core concepts** — short, consumer-focused terms and guarantees.
5. **Configuration** — real options, defaults, limits and safe values only.
6. **Usage** — one or two realistic tasks tied to runnable sample files.
7. **Safety and limitations** — duplicate/collision/security/recovery caveats next to relevant examples.
8. **Sample app** — exact path, prerequisites, `dotnet run` command and expected output.
9. **Testing & compatibility** — .NET 8/10, how to run checks; no invented benchmark claims.
10. **Versioning/license** — only verified metadata, no invented license.

README acceptance: every referenced public API, command, path, package ID, and option must be verified against implementation and/or compiled sample. Neither imaginary snippets nor code copied from old designs qualify. All examples use dummy/demo data, never secrets. This README must be complete **last**, after DI/sample are validated. The release readiness gate fails when documentation is stale or speculative.


---

## Established Stage-0 V1 architecture (retained source)

# TransactionalMessaging V1 — Architecture Specification

## 1. Purpose

`TransactionalMessaging` provides a reliable bridge between a local business transaction and asynchronous message publication using the Transactional Outbox pattern, plus inbox/deduplication support for consumers.

V1 targets **.NET 8 and .NET 10**.

The library's default and documented delivery model is:

```text
at-least-once publication
```

Duplicate publication is expected and must be safe.

---

## 2. Fundamental guarantee

If business data and an outbox record are committed in the same local database transaction:

> the intent to publish survives process crashes after commit.

The relay later publishes the durable intent.

However, a crash can occur after the broker accepts the message but before the relay marks the outbox record published.

Therefore:

```text
duplicate publication can occur
```

V1 must never advertise generic end-to-end "exactly once" delivery.

---

## 3. Research basis

- The Transactional Outbox pattern stores the outgoing message in the same transaction as the business change, avoiding a database/broker 2PC requirement.
- The relay can publish more than once after crash/recovery; consumers therefore need idempotency.
- Kafka's idempotent producer protects producer retry behavior, but generic exactly-once side effects outside Kafka require cooperation from destination storage.
- PostgreSQL exposes `FOR UPDATE ... SKIP LOCKED` for queue-like concurrent consumers.
- SQL Server `READPAST` is documented for queue/work-item patterns, with important isolation-level caveats.
- EF Core transaction/savepoint behavior contains provider/runtime details that must be tested rather than abstracted away by assumption.

See `05-Research-Sources.md`.

---

## 4. Scope

### V1 includes

- transactional outbox writing;
- provider-specific durable storage;
- concurrent dispatcher;
- claim/lease/fencing;
- retry scheduling;
- poison/dead-letter state;
- cleanup;
- message headers/metadata;
- inbox/deduplication;
- optional per-key ordered mode;
- hosting integration;
- observability;
- KafkaHighThroughput sample integration.

### V1 does not include

- distributed 2PC;
- broker-specific core dependency;
- generic saga orchestrator;
- event sourcing;
- business workflow engine;
- global exactly-once guarantee;
- global total ordering;
- automatic schema registry.

---

## 5. Package architecture

Recommended:

```text
TransactionalMessaging.Core
TransactionalMessaging.Hosting
TransactionalMessaging.SqlServer
TransactionalMessaging.PostgreSql
```

KafkaHighThroughput remains an external project and is demonstrated by a sample.

Do not make `TransactionalMessaging.Core` depend on Kafka.

---

## 6. Outbox write model

The application must be able to write business state and outgoing messages in one local transaction.

Support:

- EF Core bridge;
- raw `DbConnection` / `DbTransaction` integration.

Conceptual operation:

```text
BEGIN DATABASE TRANSACTION

  mutate business state
  insert outbox message(s)

COMMIT
```

If rollback occurs:

```text
business state absent
outbox message absent
```

If commit succeeds:

```text
business state durable
outbox intent durable
```

This is the central invariant.

---

## 7. Outbox record

Recommended logical fields:

```text
MessageId
MessageType
MessageVersion

Payload
ContentType
Headers

CorrelationId
CausationId

OrderingKey?
OrderingSequence?

State
AttemptCount
NextAttemptAt

ClaimToken?
ClaimedBy?
ClaimedUntil?

OccurredAt
CreatedAt
PublishedAt?

LastErrorCode?
LastErrorSummary?
```

Do not persist full exception stacks indefinitely by default.

---

## 8. Message identity

`MessageId` must be globally unique within the producer's outbox domain.

V1 should use application-provided IDs or generate RFC-compatible random UUIDs/Guid values.

Do not derive identity from message payload hashes.

Duplicate payloads may legitimately represent separate business events.

---

## 9. Stable type identity

Do **not** persist raw CLR assembly-qualified names as the external message type contract.

Use:

```text
MessageType = stable logical name
MessageVersion = explicit version
```

Example:

```text
wallet.withdrawal.requested
1
```

Serializer/deserializer mapping is application/integration responsibility.

This prevents class renaming or assembly changes from silently breaking persisted messages.

---

## 10. Serialization

Default representation:

```text
UTF-8 JSON
```

behind a serializer abstraction.

Safety:

```text
default payload maximum: 256 KiB
hard library ceiling: 1 MiB
default total header maximum: 16 KiB
```

Applications needing large documents should publish references, not turn the outbox into blob storage.

No unsafe polymorphic type-name deserialization.

---

## 11. State model

V1 states:

```text
Pending
Claimed
Published
DeadLettered
```

Retry does not require a separate state. A failed attempt returns to:

```text
Pending
```

with:

```text
AttemptCount
NextAttemptAt
LastError...
```

This keeps the state machine small and auditable.

---

## 12. Claiming architecture

### Core decision

Claiming is a semantic provider operation, not generic CRUD.

Conceptual API:

```text
ClaimBatch(now, worker, count, lease)
MarkPublished(claimToken, ...)
ScheduleRetry(claimToken, ...)
MoveToDeadLetter(claimToken, ...)
RenewLease(claimToken, ...)
```

### Why provider-specific implementations are required

Database queue semantics differ.

PostgreSQL can use queue-style row locking such as:

```sql
FOR UPDATE SKIP LOCKED
```

SQL Server can use a carefully tested combination based on:

```text
UPDLOCK
READPAST
ROWLOCK
```

subject to isolation/snapshot rules.

Do not hide these differences behind a "universal EF query" and assume identical semantics.

---

## 13. Lease and fencing

A claimed row contains a unique `ClaimToken`.

Every state mutation by a dispatcher is conditional on that token.

This prevents:

```text
Worker A lease expires
Worker B reclaims
Worker A wakes up late
Worker A incorrectly marks Worker B's claim published
```

### Defaults

Suggested initial defaults:

```text
batch size: 100
lease duration: 2 minutes
dispatcher concurrency: min(ProcessorCount, 8), configurable
```

Exact values are two-way-door configuration and must be benchmarked.

### Lease renewal

If publication can approach the lease duration, renew before approximately half the lease expires.

Prefer small batches and bounded publication duration over extremely long leases.

---

## 14. Publish flow

```text
1. claim a bounded batch in a short DB transaction
2. commit claim transaction
3. publish outside the DB transaction
4. broker acknowledges
5. mark Published using ClaimToken
```

Do **not** keep database row locks open while waiting on a broker network call.

### Expected duplicate window

If the process crashes between steps 4 and 5:

```text
message may be published again
```

That is correct at-least-once behavior.

---

## 15. Retry policy

Default approach:

```text
exponential backoff + full jitter
```

Suggested:

```text
initial: 1 second
cap: 5 minutes
max attempts: 20
```

These values are configurable.

Do not retry synchronously in a tight loop.

Retry classification should distinguish:

- transient transport failure;
- known non-retryable serialization/configuration problem;
- cancellation/shutdown;
- unknown exception.

A permanent serialization/configuration defect should reach a visible dead-letter state rather than consume CPU forever.

---

## 16. Dead-letter policy

Dead-letter is not silent deletion.

A dead-lettered record remains queryable with:

- message identity/type;
- attempts;
- last failure summary;
- timestamps;
- ordering metadata.

Default:

```text
do not automatically purge DeadLettered rows
```

Application operators must explicitly configure retention/archival if desired.

Metrics/alerts should make dead letters visible.

---

## 17. Ordering

### 17.1 No global ordering guarantee

V1 does not promise total global ordering.

That would conflict with horizontal dispatch and throughput.

### 17.2 Optional strict per-key mode

V1 supports an explicit ordered contract when both are supplied:

```text
OrderingKey
OrderingSequence
```

Example:

```text
OrderingKey = wallet:12345
OrderingSequence = 104
```

The application/domain owns the sequence because aggregate/domain version is more meaningful than database insertion time.

Provider behavior in ordered mode:

- do not publish sequence `N+1` while a lower sequence for the same key remains uncompleted;
- do not allow multiple dispatchers to publish the same key concurrently;
- preserve broker partition/routing key where the transport supports it.

### 17.3 Poison message in an ordered stream

Safe default:

```text
dead-lettered head blocks later messages for the same OrderingKey
```

Operators/application reconciliation must explicitly skip/resolve it.

Continuing automatically could corrupt domain order.

This strict mode will cost throughput; applications should enable it only where the domain requires ordering.

---

## 18. Inbox/deduplication

V1 includes an inbox mechanism for incoming message processing.

Identity:

```text
ConsumerScope + MessageId
```

Optionally include a payload fingerprint to detect a producer reusing one message ID for different content.

Strongest pattern:

```text
BEGIN DATABASE TRANSACTION

  reserve/check inbox message
  mutate business state
  mark inbox completed

COMMIT
```

On duplicate:

```text
business handler is not executed again
```

Do not hard-depend on OperationGuard.Core in V1. The concepts overlap, but inbox records and HTTP replay have materially different responsibilities.

---

## 19. Broker abstraction

Core defines a minimal publisher contract such as:

```text
PublishAsync(OutgoingMessage, CancellationToken)
```

The contract returns a clear publication result or throws categorized transport exceptions.

Avoid exposing Kafka/RabbitMQ client objects in core abstractions.

### KafkaHighThroughput sample

Create a final sample demonstrating:

```text
business DB transaction
    -> TransactionalMessaging outbox
    -> dispatcher
    -> KafkaHighThroughput producer
    -> consumer
    -> inbox + business transaction
```

Document:

```text
at-least-once publication + idempotent consumer
```

Do not label this generically "exactly once."

---

## 20. Hosting

`TransactionalMessaging.Hosting` uses `BackgroundService` or equivalent host integration.

Requirements:

- proper scoped-service creation;
- graceful cancellation;
- bounded loop;
- no `Task.Run` fire-and-forget leaks;
- error backoff when database itself is unavailable;
- shutdown attempts to stop claiming new work and finish/cancel bounded in-flight work.

The dispatcher must never swallow fatal configuration errors and continue appearing healthy.

---

## 21. Backpressure

Safety rules:

- bounded claim batch;
- bounded parallel publish count;
- bounded channel/queue if an internal queue is used;
- no unbounded accumulation of payload objects in memory;
- pause/reduce claims when broker publication falls behind;
- expose backlog age and count metrics.

---

## 22. Cleanup

Published rows are operational history, not the authoritative business audit log.

Suggested default:

```text
Published retention: 7 days
```

Configurable.

Cleanup:

- deletes in bounded batches;
- never touches Pending/Claimed;
- never deletes DeadLettered by default;
- respects database load;
- has metrics for duration and deleted rows.

Inbox retention must be **at least as long as the maximum plausible redelivery/replay window** of the transport/business workflow.

Deleting inbox records too early re-enables duplicate business execution.

---

## 23. Observability

Minimum metrics:

```text
outbox_pending_count
outbox_oldest_pending_age
outbox_claimed_count
outbox_publish_success_total
outbox_publish_failure_total
outbox_retry_total
outbox_deadletter_total
outbox_publish_latency
inbox_duplicate_total
dispatcher_claim_latency
cleanup_deleted_total
```

Trace correlation:

- MessageId;
- CorrelationId;
- CausationId;
- safe logical MessageType.

Do not put full payloads into spans/logs by default.

---

## 24. Failure model

Mandatory scenarios:

### Producer transaction

- crash before business commit;
- rollback;
- commit succeeds;
- outbox insert failure.

### Dispatcher

- crash before claim;
- crash after claim;
- crash during publish;
- crash after broker ACK;
- crash before mark-published;
- stale worker after lease expiry.

### Infrastructure

- broker outage;
- database outage;
- slow broker;
- partial timeout/ambiguous broker outcome;
- clock skew;
- process shutdown;
- host kill.

### Volume

- million-row backlog;
- poison message;
- retry storm;
- multiple dispatchers;
- hot ordering key;
- cleanup while dispatching.

---

## 25. Core invariants

1. A rolled-back business transaction exposes no publishable outbox message.
2. A committed business transaction with an outbox insert retains durable publication intent.
3. At most one valid claim token controls a row at a time.
4. A stale token cannot mutate a re-claimed row.
5. Broker ACK followed by process crash may cause duplicate publication but not message loss.
6. The library never documents generic exactly-once delivery.
7. Retry loops are bounded/backed off.
8. Dead-letter records are visible and retained by default.
9. Outbox cleanup never deletes unpublished work.
10. Inbox completion can participate in the same local transaction as business effects.
11. Strict ordered mode cannot advance past unresolved lower sequence without explicit operator/application action.
12. No unbounded in-memory queue is required for correctness.

---

## 26. Provider matrix

### SQL Server

Test:

- READ COMMITTED;
- READ_COMMITTED_SNAPSHOT configurations that affect `READPAST`;
- lock escalation pressure;
- concurrent claimers;
- transaction rollback;
- cleanup contention.

### PostgreSQL

Test:

- `FOR UPDATE SKIP LOCKED`;
- concurrent claimers;
- transaction rollback;
- connection loss;
- cleanup contention.

Provider-specific SQL is acceptable inside provider packages.

Correctness is more important than pretending portability is free.

---

## 27. EF Core caution

EF Core is an integration option, not the semantic authority.

Tests must account for:

- automatic `SaveChanges` transaction behavior;
- manually controlled transaction and execution-strategy interactions;
- savepoint behavior;
- SQL Server MARS caveat;
- shared transaction across context/ADO usage where supported.

---

## 28. Five-Hat architecture review

### White

The database and broker do not form one atomic resource in this design. Relay duplicate publication is expected.

### Red

Operators need confidence that "dead letter" or "retry" is observable rather than invisible background magic.

### Black

The dangerous bugs are silent event loss, stale workers, retry storms and false exactly-once claims.

### Yellow

One robust implementation replaces ad hoc "save then publish" logic across many future services.

### Green

Use short DB claims + network publication outside the DB transaction rather than long row locks or distributed 2PC.

---

## 29. Reversibility register

| Decision | Door | Notes |
|---|---|---|
| At-least-once contract | One-way semantic promise | Stronger scoped guarantees can be added later without invalidating it |
| Four outbox states | One-way-ish persisted schema | Keep small |
| Provider-specific claiming | Two-way implementation | Hidden behind semantic store contract |
| SQL Server + PostgreSQL V1 | Two-way | More providers later |
| Per-key ordered mode | One-way public semantics | Sequence supplied by domain prevents hidden ordering assumptions |
| No broker core dependency | One-way architectural boundary | Preserves reuse |
| Dead-letter retention default | Two-way configuration | Safety-biased default |

---

## 30. Project layout

```text
src/
  TransactionalMessaging.Core/
    Abstractions/
    Models/
    Options/
    Serialization/
    Diagnostics/
    Exceptions/
    Validation/
    Internal/

  TransactionalMessaging.Hosting/
    Dispatching/
    Cleanup/
    Registration/
    Extensions/
    Internal/

  TransactionalMessaging.SqlServer/
    Outbox/
    Inbox/
    Schema/
    Registration/
    Internal/

  TransactionalMessaging.PostgreSql/
    Outbox/
    Inbox/
    Schema/
    Registration/
    Internal/

tests/
  TransactionalMessaging.Core.Tests/
  TransactionalMessaging.SqlServer.IntegrationTests/
  TransactionalMessaging.PostgreSql.IntegrationTests/
  TransactionalMessaging.ConcurrencyTests/
  TransactionalMessaging.FaultInjectionTests/
  TransactionalMessaging.LoadTests/
  TransactionalMessaging.SecurityTests/
  TransactionalMessaging.Benchmarks/

samples/
  TransactionalMessaging.KafkaHighThroughput.Sample/
```

---

## 31. Release gate

V1 cannot release until:

- local-transaction invariants are proven on both DB providers;
- stale-lease/fencing tests pass;
- multi-dispatcher claim tests pass;
- forced-crash-after-broker-ACK test demonstrates duplicate-not-loss behavior;
- retry storms remain bounded;
- ordered-key tests pass;
- inbox duplicate tests pass;
- million-row/backlog test meets documented operational expectations;
- messaging-specific adversarial reviewer returns PASS;
- no unresolved Critical/High findings remain;
- documentation consistently says at-least-once unless describing a narrower proven scope.
