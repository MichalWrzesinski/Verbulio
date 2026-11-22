# Critical path verification plan

Scope and rationale are defined in the [product overview](../product/overview.md). Run complete paths in an isolated browser environment with PostgreSQL and locally captured mail. All three are required before the first release.

| ID | Preconditions and dependencies | Steps and expected results | Readiness |
| --- | --- | --- | --- |
| CP-001 | Reviewed placement bank, anonymized guest identity, account registration and learning. [Placement engine](../tasks/TASK-002-placement-engine.md) and onboarding integration. | Start as a guest and refresh halfway. Finish 20 distinct questions with no correctness feedback. Register with the server's estimate and start a first session. Replay a claim from another guest and verify rejection. | Awaiting implementation |
| CP-002 | Two test accounts, a controlled library, PostgreSQL and a controlled clock. [Local learning slice](../tasks/TASK-001-learning-session.md) and real accounts. | Start with due words. Answer, refresh and retry an unacknowledged submission. Complete and resume on a later day. Verify due order, ownership, preserved feedback and exactly one schedule change per word. End early in a separate session and verify no streak credit. | Awaiting implementation |
| CP-003 | Two isolated accounts, captured email, an operator identity and controlled time. Account lifecycle and retention increments. | Confirm and recover access via one-time links. Verify access denial across owners. Exercise verified export and password-confirmed deletion, then check session invalidation and scope of remaining data. | Awaiting implementation |
