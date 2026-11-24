# TASK-027 Email confirmation API

Status: DONE

Redeem the token with a guarded HTTPS POST so opening an email link does not consume it. Accept send requests only from authenticated accounts. Resolve the recipient from the same atomic update that issues the token and enforce a one-minute per-account interval. Requests skipped during the interval and submissions with unknown provider outcomes return an acknowledgement without claiming delivery.

Configure the optional send path with `EmailConfirmation__SendEnabled`, `EmailConfirmation__BrevoApiKey`, `EmailConfirmation__Sender` and `EmailConfirmation__PageUrl`. The latter is the full HTTPS URL of the frontend confirmation page. The token is passed in the link fragment and should be removed from browser history by that page. Leave delivery disabled until the frontend and a designated delivery test are ready. The seven-day learning rule is separate.
