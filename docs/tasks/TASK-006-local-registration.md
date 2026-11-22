# TASK-006 Local registration and placement claim

Status: DONE

## Goal

Exercise the server-side account creation and single-use placement result segment of CP-001. Use the existing loopback development identity and fixture PostgreSQL. Login, browser forms, mail and public abuse protection remain separate.

## Acceptance

- Register only with a completed, currently owned test and establish the starting level from the persisted server result.
- Atomically save the account and mark the test claimed. Concurrent requests claim once, and a failed account insert leaves the result available to retry.
- Reject stale, missing, foreign, unfinished and consumed tests. Prevent restarting a claimed test.
- Reject unknown or malformed inputs. Normalize email for lookup and store only a framework password hash. The response exposes only the account ID and starting level.


## Verification

CP-001, local account creation. The Release build and all 55 .NET tests passed. PostgreSQL integration covered unauthorized, foreign, unfinished, missing and consumed tests, strict input, a simultaneous claim, case-insensitive duplicate email and successful retry after rollback. An upgrade from migrations 001 and 002 preserved a previously stored test. The local database check verified Identity hashes and matching claimed levels for both created accounts.

## Report

The development endpoint and existing fixture identity exercise account creation without real email delivery. Angular continues to show the current test if restart cannot proceed. Login, the registration form, Brevo, and public abuse protection remain separate.
