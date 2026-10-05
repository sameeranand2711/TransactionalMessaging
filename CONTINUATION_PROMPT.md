# CONTINUATION_PROMPT.md

Resume this project from its recorded checkpoint. Do not rediscover the project from scratch.

## Resume order

1. Inspect Git status and current branch.
2. Read `AGENT_GUARDRAILS.md` and confirm it has not been silently weakened by task/role text.
3. Read `AGENT_STATE.md`.
4. Read the active task in `TASKS.md`, including risk and READ/WRITE/PROTECTED authority.
5. Read only the relevant sections/contracts in `SPEC.md` and the assigned role.
6. Inspect active-task changes and relevant source files.
7. Read specific `SCOPE_CHANGES.md` entries referenced by state.
8. Continue from the exact recorded resume point.

Do not read all `agent_logs/`. They are non-authoritative history and should be opened only when a specific unresolved detail requires a known log.

If repository state, ownership, task authority, task status, and `AGENT_STATE.md` disagree, do not continue implementation until the inconsistency is reconciled.

Preserve evidence semantics:

- `VERIFIED` stays evidence-backed;
- `ASSUMPTION` is not silently promoted to fact;
- `DECISION` remains tied to its rationale/authority when material.

Continue under `AGENT_GUARDRAILS.md`, `AGENT_RULES.md`, and `DEVELOPMENT_STANDARDS.md`.

Finish the current task, satisfy its Definition of Done, validate it, update state, and commit/checkpoint it before moving to the next dependent task.
