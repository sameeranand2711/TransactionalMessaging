# TASKS.md

Tasks are sequential. `NOT_STARTED → IN_PROGRESS → COMPLETE`; `BLOCKED` is a checkpoint, not approval to bypass a gate. High-risk tasks require targeted independent review to be counted complete; reviewers may review a related high-risk diff as one bounded pass. No next dependent task starts without its gates.

## Shared task boundaries
- **Default external side effects:** `NONE` for every task.
- **Production authority:** `NONE` for every task.
- **PROTECTED:** inherit `AGENT_GUARDRAILS.md`, including live data, secrets, infra, unrelated libraries, and other active tasks.
- A branch-local commit after completed tasks is authorized; remote push/PR/publish/merge are not.
- File paths refer to conventional structure; inspect real repository locations first and adapt only within the same logical scope. Record deviations in `AGENT_STATE.md`.

## TM-01 — Core contracts and provider-backed transaction safety

- **Status:** `NOT_STARTED`
- **Owner:** `agent-01-core-storage`
- **Dependencies:** None
- **Risk:** `HIGH`
- **External side effects:** `NONE`
- **Production authority:** `NONE`

**READ:** `SPEC.md`; existing solution/core/store project files; directly relevant tests, provider schemas

**WRITE:** `src/TransactionalMessaging.Core/**`, `src/TransactionalMessaging.SqlServer/**`, `src/TransactionalMessaging.PostgreSql/**`, `tests/**` limited to storage/contract coverage

**PROTECTED:** `AGENT_GUARDRAILS.md` default areas, all unrelated components and external systems.

**In scope:** Implement or finish stable message envelope, serializer limits, atomic local business + outbox write contract, provider-specific claim/fencing/inbox store semantics, and real SQL provider contract tests. Inspect existing implementation first and reuse it. Do not edit unrelated components.

**Out of scope:** unrelated refactoring, new domain features, production/shared infra, external writes and speculative new abstractions.

**Outputs:** Core/provider code and tests with verified .NET 8/10 builds.

**Acceptance criteria:** Rolled-back DB transaction has no publishable event; committed transaction retains outbox intent; one valid claim token; stale claims cannot complete; SQL Server and PostgreSQL provider tests cover concurrency; source only advertises at-least-once.

**Validation:** Build and contract-test both targets; local disposable SQL Server/PostgreSQL integration where available. If database unavailable, mark required tests BLOCKED rather than fake PASS.

**Definition of done:**
- [ ] Implemented required observable behavior and outputs; acceptance satisfied.
- [ ] READ/WRITE/PROTECTED and command boundaries respected.
- [ ] No external/production side effects or unapproved dependencies.
- [ ] Tests/validations pass without weakening tests or disabling checks.
- [ ] Correct risk-based independent review completed when required; no unaddressed blocker.
- [ ] `AGENT_STATE.md` updated; scope changes recorded; local task checkpoint/commit when authorized.

## TM-02 — Bounded dispatch, inbox, ordering and recovery

- **Status:** `NOT_STARTED`
- **Owner:** `agent-01-core-storage`
- **Dependencies:** TM-01
- **Risk:** `HIGH`
- **External side effects:** `NONE`
- **Production authority:** `NONE`

**READ:** Relevant store contract/implementation and tests; `SPEC.md` dispatch/inbox sections

**WRITE:** `src/TransactionalMessaging.Core/**`, `src/TransactionalMessaging.Hosting/**`, provider files needed for claims/retries, relevant `tests/**`

**PROTECTED:** `AGENT_GUARDRAILS.md` default areas, all unrelated components and external systems.

**In scope:** Finish hosted dispatcher, cancellation, bounded batches, lease renewal, fencing, retries/backoff, dead letters, retention/cleanup, inbox transactional completion, and optional per-key order. No holding DB locks during network publish.

**Out of scope:** unrelated refactoring, new domain features, production/shared infra, external writes and speculative new abstractions.

**Outputs:** Runnable host/inbox logic and tests for retries, duplicate ACK/crash, stale lease and ordering.

**Acceptance criteria:** Crash after broker ACK may cause duplicate not silent loss; no unbounded memory/retry; strict ordering never skips blocked head automatically; inbox + business transaction atomicity is tested; cleanup cannot delete Pending or claimed work.

**Validation:** Targeted fault injection + concurrency/inbox tests; no invented exactly-once claims; independent review required before declaring these HIGH tasks complete.

**Definition of done:**
- [ ] Implemented required observable behavior and outputs; acceptance satisfied.
- [ ] READ/WRITE/PROTECTED and command boundaries respected.
- [ ] No external/production side effects or unapproved dependencies.
- [ ] Tests/validations pass without weakening tests or disabling checks.
- [ ] Correct risk-based independent review completed when required; no unaddressed blocker.
- [ ] `AGENT_STATE.md` updated; scope changes recorded; local task checkpoint/commit when authorized.

## TM-03 — Minimal dependency injection and console integration demo

- **Status:** `NOT_STARTED`
- **Owner:** `agent-02-integration-docs`
- **Dependencies:** TM-01, TM-02
- **Risk:** `MEDIUM`
- **External side effects:** `NONE`
- **Production authority:** `NONE`

**READ:** Actual public APIs, hosting/store setup, targeted tests and sample conventions

**WRITE:** Only DI/registration/extensions in existing appropriate src projects, `samples/TransactionalMessaging.Sample/**`, and matching registration/sample tests

**PROTECTED:** `AGENT_GUARDRAILS.md` default areas, all unrelated components and external systems.

**In scope:** Add or verify conventional AddTransactionalMessaging registration, provider and optional hosted dispatcher wiring with safe lifetimes. Create one console Generic Host demo of local DB transaction -> outbox -> simple local publisher -> inbox dedup; no Kafka deployment. Optional Kafka adapter only when API exists locally; never make it a mandatory dependency.

**Out of scope:** unrelated refactoring, new domain features, production/shared infra, external writes and speculative new abstractions.

**Outputs:** Small compilable console demo, DI tests, exact documented run prerequisites.

**Acceptance criteria:** No invented API; services resolve; duplicate service registration does not multiply hosted consumers unintentionally; console demonstrates published message and suppressed duplicate with disposable local DB; command works under documented prerequisites.

**Validation:** Compile sample on .NET 8/10 as supported; run with an explicitly local disposable DB when available; check stdout/DB evidence. Unavailable infrastructure cannot be labeled PASS.

**Definition of done:**
- [ ] Implemented required observable behavior and outputs; acceptance satisfied.
- [ ] READ/WRITE/PROTECTED and command boundaries respected.
- [ ] No external/production side effects or unapproved dependencies.
- [ ] Tests/validations pass without weakening tests or disabling checks.
- [ ] Correct risk-based independent review completed when required; no unaddressed blocker.
- [ ] `AGENT_STATE.md` updated; scope changes recorded; local task checkpoint/commit when authorized.

## TM-04 — Final consumer README, release checks and targeted independent audit

- **Status:** `NOT_STARTED`
- **Owner:** `agent-02-integration-docs + agent-03-independent-review`
- **Dependencies:** TM-03
- **Risk:** `HIGH`
- **External side effects:** `NONE`
- **Production authority:** `NONE`

**READ:** Verified src/tests/samples, package metadata, current README, task status

**WRITE:** Repository `README.md`, relevant release metadata/docs, bounded test fixes only via original owner after recorded review findings

**PROTECTED:** `AGENT_GUARDRAILS.md` default areas, all unrelated components and external systems.

**In scope:** Create/update well-structured actual README last; validate snippets by compiled demo; run broader regression and local package check. Independent reviewer examines high-risk source/changes and public guarantees once; permit one bounded remediation and targeted verification; no cyclic review loop.

**Out of scope:** unrelated refactoring, new domain features, production/shared infra, external writes and speculative new abstractions.

**Outputs:** Validated README and review PASS/FAIL evidence; local package/branch checkpoint ready for human release/merge.

**Acceptance criteria:** README accurately documents registration, usage, setup, outbox/inbox, at-least-once duplicates, cleanup and samples; unit/integration and failure checks run; no Critical/High open; independent review passed; no remote publish or merge.

**Validation:** Final build/test/static/dependency review and independent targeted code-and-doc review; one remediation pass plus targeted verification only.

**Definition of done:**
- [ ] Implemented required observable behavior and outputs; acceptance satisfied.
- [ ] READ/WRITE/PROTECTED and command boundaries respected.
- [ ] No external/production side effects or unapproved dependencies.
- [ ] Tests/validations pass without weakening tests or disabling checks.
- [ ] Correct risk-based independent review completed when required; no unaddressed blocker.
- [ ] `AGENT_STATE.md` updated; scope changes recorded; local task checkpoint/commit when authorized.
