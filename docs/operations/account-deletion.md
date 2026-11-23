# Account deletion and restoration

The preview API enables the account deletion route only with a configured, independent deletion journal. The route requires the authenticated account, current password, HTTPS, the local client header and the existing request limits. Completion revokes all login sessions and removes the account, its claimed placement test, learning sessions, word progress and credited days. The browser flow is tracked separately.

## Preview setup

Configure `ConnectionStrings__AccountDeletionJournal` for a dedicated PostgreSQL database outside the application database being restored. Specify distinct database names explicitly in both connection strings. The journal stores only account UUID and verified deletion request time. The deployment operator must preserve this database through every application backup and restore, control its credentials and agree retention covering all restorable application backups before enabling account deletion on a shared preview or public host.

Run the existing `--migrate` command with both connections to initialize their schemas. A verified deletion is committed to the journal before the application data can be removed. A failure writing the journal returns a temporary-unavailability response and leaves the application data untouched. If a verified intent was recorded and application deletion was interrupted, the same password-authenticated request can complete it while the account still exists.

## Restore and replay

Keep the restored application database disconnected from users. With the application stopped, configure `ConnectionStrings__Learning` to point to the restored database and `ConnectionStrings__AccountDeletionJournal` to the current preserved journal. Under the deployment operation lock, run:

```sh
dotnet Verbulio.Api.dll --replay-account-deletions
```

The replay removes every account associated with a verified intent, including pending intents, and can be repeated after interruption. Check the command result and verify that deleted identities and their related records are absent before returning the restored database to service. Keep the environment closed if the journal is missing or incomplete. Temporary operator export files are outside both databases and follow their separate cleanup procedure.
