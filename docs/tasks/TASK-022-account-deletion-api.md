# TASK-022 Account deletion backend and recovery

Status: DONE

Provide a password-confirmed, authenticated deletion backend. Scope all effects to the session owner, invalidate login access, and delete the claimed placement, account and learning records together. The protected route is enabled only with a distinct deletion journal database. Preserve verified intent independently before deleting, and replay the current journal into an offline restored database.

PostgreSQL verification uses only synthetic accounts. Check authorization and the local client guard, wrong password, refusal to delete while the journal is unavailable, owned row removal during concurrent login rotation, another account's continued access, and idempotent replay after restoring a pre-deletion dump. The shared VPS journal, account deletion UI and deployment activation are follow-up work. See the [runbook](../operations/account-deletion.md).
