# TASK-018 Account learning preference

Status: DONE

Persist the automatic advance preference on the account. The default is off for current and new accounts to preserve manual advance. The authenticated and owner-scoped HTTPS endpoints `GET` and `PUT /api/v1/local-account/settings/learning` use `{"autoAdvance":true}` or `{"autoAdvance":false}`. The write requires the existing local client header. Missing, null or foreign properties are rejected. The settings view and feedback behavior are a separate increment.

PostgreSQL verification covers default, repeat writes, explicit off, persistence through sign-out and sign-in, same-level owner separation, and guest and signed-out denial. The additive migration applies the default to existing accounts.
