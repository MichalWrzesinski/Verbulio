# TASK-023 Account deletion confirmation

Status: DONE

Use the J-4 comic settings dialog and success screen for account deletion. Show the action only when the authenticated account settings response includes `deletionAvailable: true`. Require the current password and confirm success only after HTTP 204. After confirmation, clear the tab's account practice state. Leave backup-specific timing to the deployed retention policy.

Verify cancellation, invalid passwords, a confirmed deletion, reauthentication, and the settings response with and without a configured journal. Run destructive checks only against isolated synthetic accounts. VPS activation depends on the [account deletion runbook](../operations/account-deletion.md).
