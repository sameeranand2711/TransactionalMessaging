# TransactionalMessaging agent pack (agents-pack-skill v0.2.0)

This folder contains **instructions only**, not the implemented library, DI extensions, runnable example or final repository README.

**Use:** Copy/extract these contents into the intended repository working directory (or configure Codex to read them from this folder) and start with `INITIAL_PROMPT.md`. Existing repository code/README must be inspected before changes. On quota reset, resume with `CONTINUATION_PROMPT.md`.

**Scope:** TransactionalMessaging only. `TASKS.md` owns work order/permissions, `SPEC.md` owns contracts, and `AGENT_GUARDRAILS.md` is the hard authority boundary. The independent reviewer operates in a separate role/session; no agent may spawn another agent.

**Expected final project outputs:** functioning V1, clean DI registration, one small verified console sample, tested failure/safety contract, and a **well-structured project `README.md` written or updated at the end**. No placeholder README is shipped in this pack.

**Practical constraints:** use .NET 8/.NET 10 as appropriate, local-only safe tests, concise state/handoff, at most two materially different retries per blocker, one review + one remediation + targeted verification. Never automatically merge/push/release.
