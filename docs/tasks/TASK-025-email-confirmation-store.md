# TASK-025 Email confirmation storage

Status: DONE

Support issuing and consuming email confirmation tokens in PostgreSQL. Generate a random link token, store its digest, use a 24-hour validity period, and replace any outstanding token when issuing a new one. An atomic update records confirmation and clears the token so concurrent submissions cannot both succeed. Existing accounts keep their confirmation state.

Verify expiry, replacement, replay, competing confirmations and owner isolation with synthetic accounts in an isolated database schema. Mail delivery, endpoints, views and the seven-day learning rule are subsequent increments.
