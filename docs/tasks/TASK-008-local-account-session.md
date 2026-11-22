# TASK-008 Local account session

Status: DONE

## Goal

Exercise local login and logout with an account created after placement. Verify Identity hashes and use an ASP.NET Core cookie with a PostgreSQL-backed session. Keep guest and account identities separate.

## Acceptance

- Return the same 401 response for unknown account and wrong password. Normalize the email as at registration and limit attempts.
- Successful login creates an account session that survives an API restart and has a fixed expiry. Guest cookies alone cannot access the account session.
- A new login rotates the session. Logout revokes server-side access even if a browser still presents a copy of the cookie.
- Verify session ownership, restart, expiry and logout in PostgreSQL. The fixture stays on the loopback interface.


## Verification

CP-001 and CP-003, local account session segment. The Release build and all 55 .NET tests passed. PostgreSQL integration covered wrong and unknown credentials, guest/anonymous access denial, email and IP rate limits, normalized login, restart, rotation and server revocation. A local fixture expired a stored session while its browser cookie was still present and verified denial.

## Report

The development account API can log in, inspect and revoke its server-backed session. The login form and account-owned learning are separate work. There is no remote environment change in this task.
