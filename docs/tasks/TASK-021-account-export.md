# TASK-021 Operator account export

Status: DONE

Provide a CLI-only, UUID-scoped export of an account's contact, preference, placement, word progress, learning sessions and credited days. The query uses a consistent statement snapshot and an explicit field allowlist. Exclude credentials, cookie and login tokens, technical submission keys and other accounts. Create a private JSON file via a staged write and preserve existing destinations. The operator verifies identity outside the CLI.

Verify with real synthetic account data in isolated PostgreSQL including a separately scoped second account, no matching account, private permissions and an existing destination. No request flow, actual personal data delivery or public UI change is included. See the [operator instructions](../operations/account-export.md).
