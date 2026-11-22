# TASK-010 Account learning session

Status: DONE

## Goal

Exercise CP-001 and CP-002 from an authenticated local account through the first practice session. Select from the pilot bank at the server-owned starting level and persist answer history under the account owner. Keep older fixture sessions readable.

## Acceptance

- Account routes require the HTTPS account scheme. The guest cookie and other accounts cannot read, submit or end the session.
- A new account receives ten distinct items from its starting level. The same account can resume a question after logout and login. Its separately registered account has separate progress.
- Reuse the practice screen, including answer feedback, summary and interruption. Provide an entry from the signed-in page and a return to the account.


## Verification

CP-001 and CP-002, local first account practice. The 55 .NET tests, clean Release build, PostgreSQL integration and Angular builds and tests passed. PostgreSQL verifies guest and cross-account denials, A1/C2 selection, ten distinct items, sign-out and sign-in resume, completion and separate progress. The next session excludes reviewed words whose repeats are not due.

Chrome completed the local flow from registration to an account-backed first session, including a wrong answer that returned, sign-out and sign-in, and a 9-of-10 summary. Chrome held a successful start response across in-app navigation and sign-out, then verified that releasing it left the tab cache empty. At 320, 375 and 1280 px the screen used the existing J-2 component and had no horizontal overflow. Axe found no violations in the rules checked on practice and summary. Chrome reported no unexpected responses or application console warnings.

## Report

The HTTPS development fixture supports account-owned practice and resume. Category, profile/streak and public account workflows remain separate.
