# TASK-020 Contact and sign-out in settings

Status: DONE

Extend the J-4 settings view with the account's registered email address and a sign-out action. An owner-scoped HTTPS `GET /api/v1/local-account/settings/account` returns only `email` and uses the existing no-store response policy. The address is preserved as registered. The learning preference response and practice traffic remain free of contact data.

Reuse the session client for sign-out from settings and from the sign-in view. On a lost sign-out response the client checks the session, clears the practice tab state when sign-out is confirmed, and only then returns from settings to the landing page. Account export and deletion are separate increments.

Verification covers PostgreSQL owner isolation, an attempted owner override, guest and signed-out denial, Angular builds and tests, and Chrome coverage of contact read recovery and sign-out after a lost successful response. The J-4 contact and account sections follow the preview scope.
