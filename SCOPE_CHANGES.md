# SC-001: SQL Server LocalDB not available for integration tests

- **Status:** DISCOVERED
- **Impact:** BLOCKING — Cannot verify transaction atomicity contract (SPEC.md section 25 invariant #1 and #2)
- **Severity:** HIGH — Core acceptance criteria requires provider contract tests
- **Discovery:** Integration test run failed with "Cannot open database" / "Login failed"
- **Root cause:** LocalDB not installed or configured on test machine
- **Effort:** Low (install LocalDB) to Medium (configure alternative disposable SQL Server)
- **Authority change required:** None — tests are within approved WRITE scope
- **Evidence:** Test output shows SqlException "Cannot open database TransactionalMessagingTests"
- **Options:**
  1. Install SQL Server LocalDB
  2. Use Docker SQL Server container with disposable database
  3. Use external test SQL Server instance (requires connection string config)
  4. Mark integration tests as environment-dependent and document prerequisites
- **Decision needed:** Approve option 4 (document prerequisites) and continue with PostgreSQL tests, or block until database available

Per AGENT_GUARDRAILS.md section 17: "Stop/checkpoint rather than improvising when... a material repository fact cannot be verified and guessing could affect correctness."

Per TASKS.md TM-01 validation: "If database unavailable, mark required tests BLOCKED rather than fake PASS."
