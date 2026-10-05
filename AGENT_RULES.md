# AGENT_RULES.md

> Operational governance for this pack. Hard authority/safety rules live in `AGENT_GUARDRAILS.md` and cannot be weakened here.

## Execution priorities

1. Correct completion.
2. Authority/scope containment.
3. Cost/token efficiency.
4. Verification/auditability.
5. Speed.
6. Specialization.

## Scope

Work only on the active approved task and within its READ/WRITE/PROTECTED boundaries.

Do not implement unrelated cleanup, refactors, upgrades, features, architecture changes, or optimizations.

If out-of-scope work is discovered, use `SCOPE_CHANGES.md`.

## Risk classification

- `LOW`: docs/copy/isolated non-sensitive work with no meaningful contract/environment impact.
- `MEDIUM`: feature logic, API/persistence/cross-component changes, new dependencies, or meaningful contract changes.
- `HIGH`: auth/authz, payments/financial logic, cryptography, security controls, concurrency/distributed consistency, migrations, infrastructure, production, secrets, destructive operations, or high-impact compatibility changes.

Review policy:

- LOW: executor self-review.
- MEDIUM: executor self-review; independent targeted review only when boundary/risk warrants it.
- HIGH: independent targeted review is mandatory before completion.

Risk does not automatically justify more agents. Normal agent-count gates still apply.

## Task sequencing

- Complete the active task before starting the next dependent task.
- Meet acceptance criteria and Definition of Done.
- Perform required validation.
- Update state.
- Commit/checkpoint as required.
- Then proceed.

## Context discipline

Normal read order:

1. `AGENT_GUARDRAILS.md`
2. `AGENT_STATE.md`
3. active task in `TASKS.md`
4. relevant `SPEC.md` sections/contracts
5. assigned role file
6. relevant source files
7. targeted searches

Do not repeatedly scan the whole repository.

Do not read all `agent_logs/` during normal continuation.

## Evidence discipline

For material continuation facts:

- `VERIFIED`: evidence-backed; include a concise pointer when useful.
- `ASSUMPTION`: unverified; state impact/verification need when material.
- `DECISION`: approved choice; state rationale/authority when material.

Do not present assumptions as verified facts.

## State

Keep `AGENT_STATE.md` concise and resumable, ideally within 100–180 lines.

Move obsolete execution detail to `agent_logs/`, but never move correctness-critical decisions/evidence solely into ignored logs.

## Retry limit

Maximum two **materially different** unsuccessful attempts for the same blocking implementation/debugging issue.

After the second failure:

- stop implementation on that blocker;
- record attempts and concise evidence;
- identify likely cause/unknown;
- mark task `BLOCKED` if necessary;
- propose the smallest next diagnostic step;
- escalate rather than continuing blind iteration.

Minor variations of the same approach do not reset the count.

## Review limit

When independent review is required/justified:

- one targeted review;
- one remediation pass;
- one targeted verification of identified findings.

If blocking findings remain, stop and escalate.

No automatic repeated review-fix loops.

## Testing

- Cheap task-specific validation after each task.
- Targeted regression after roughly three related completed tasks.
- Broader regression at phase/release/PR boundaries.
- High-risk changes may require immediate targeted regression.
- Follow `AGENT_GUARDRAILS.md` test-integrity rules; never weaken validation merely to pass.

## Git

Before development:

- inspect repository/branch status;
- initialize Git if needed and appropriate;
- ensure `.gitignore`;
- ignore `agent_logs/`;
- exclude project-appropriate build/cache/IDE/temp/secret artifacts;
- use the approved feature/fix branch.

Commit after each fully completed bounded task when local commit authority is granted.

Never auto-merge. Never force-push/rewrite shared history unless the explicit approved task grants that authority.

## Command and external-action policy

Use the least authority required.

Non-destructive local inspection/build/test commands are normally allowed when relevant. Environment mutation, external writes, deployment, production changes, destructive commands, machine-global changes, and credential changes require explicit task authority according to `AGENT_GUARDRAILS.md`.

External side effects default to `NONE`.

## Models

Use the least expensive adequate model.

Premium/expensive model use requires explicit approval after explaining:

- why current capability is insufficient;
- remaining work;
- expected benefit;
- effort class;
- token/context impact;
- cost estimate when responsibly knowable;
- fallback if denied.

## Agent creation

Agents cannot spawn, invoke, or delegate to subagents.

If another specialist seems necessary, record the need and escalate to the controller/user.

## Controller/orchestrator

When one exists, its default job is coordination:

- assign/select ready work;
- enforce dependencies, authority, risk gates, and budgets;
- coordinate shared ownership;
- reconcile state;
- trigger approved review gates;
- surface decisions.

It must not silently absorb implementation/specialist work. Reassignment must be explicit and recorded.

## Parallelism

Sequential execution is the default.

Parallel work is allowed only when:

- shared contracts are stable;
- WRITE scopes are disjoint or explicitly coordinated;
- there are no output/dependency conflicts;
- external side effects cannot conflict;
- duplicated context does not erase the benefit.

## Scope changes

For newly discovered out-of-scope work:

- log a concise entry in `SCOPE_CHANGES.md`;
- reference it in `AGENT_STATE.md`;
- classify blocking/non-blocking and risk where material;
- do not deeply investigate before approval;
- continue only if current acceptance criteria remain achievable;
- otherwise checkpoint and request a decision.

## Development quality

Follow `DEVELOPMENT_STANDARDS.md`.

Prefer minimal diffs, clear code, contextual engineering practices, verified repository facts, and explicit evidence over assumptions.

## Agent count and handoff for this pack

- Deliberately no orchestration/controller agent; execute roles/tasks in dependency order from `TASKS.md`.
- The assigned agent may not invoke another agent. A review handoff means checkpoint, then start the named reviewer as a separate independent session or through an explicitly provided host-level role switch (not agent spawning).
- If host-level role switching is not available, print the minimal handoff prompt rather than pretending independent review ran. Do not ask for routine engineering approval at ordinary task transitions.
- Once a reviewer finds a blocker, the implementation owner gets one bounded remediation pass; reviewer performs only targeted verification afterward. If still blocked, stop and record evidence.
- The README is finalized **last**; do not generate a placeholder at startup.
