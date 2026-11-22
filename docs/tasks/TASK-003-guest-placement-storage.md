# TASK-003 Local guest placement persistence

Status: IN_PROGRESS

## Goal

Exercise CP-001 placement start, answers, resume, controlled restart and server-owned result through the development API and PostgreSQL. Reuse the local fixture identity and a synthetic bank. Real registration, the single-claim transaction with an account, and the reviewed bank are separate work.

## Acceptance

- Return the same test and stable question on resume and process restart. Explicit restart opens a new test and supersedes the old one.
- Ask exactly 20 questions, withhold grades and persist the level only with the final answer.
- Deny anonymous and cross-owner access to every test operation. Reject unknown input and conflicting keys. Simultaneous retries advance a test only once.
- Run an isolated PostgreSQL integration test as part of the current CI backend checks.

## Verification

CP-001, server-side segment. The existing PostgreSQL integration command exercises both local API flows. It checks guest isolation and rejected writes, explicit restart, concurrent repeats, resume after process restart, and all 20 responses through the persisted result.

## Report

49 .NET tests, the Release build and the PostgreSQL 18 integration check passed locally. The development adapter has no user-facing route. It replays the pinned synthetic bank and uses the existing development cookie. Production placement still depends on public guest identity, a reviewed vocabulary bank, retention, onboarding and the account transaction.
