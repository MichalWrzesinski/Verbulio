# TASK-001 Local learning session

Status: IN_PROGRESS

## Goal and scope

Deliver one repeatable learning session through Angular, the ASP.NET Core API and PostgreSQL on controlled bilingual words. Use the existing comic mockup components. Use isolated development identities until the account module is available.

## Acceptance

- Select distinct words and show four choices or typed entry as dictated by progress. Alternate EN→PL and PL→EN each time a word appears.
- Grade on the server, return wrong words within the available queue, and apply the Leitner schedule once for each word in a session. Count the first resolved answer per word in the final score.
- Preserve the active question and confirmed feedback on refresh. Persist partial progress on early exit. Deny access to another identity's session and deduplicate concurrent answer retries, including after server restart.
- Render the learning panel at 320, 375 and 1280 px without horizontal overflow. Verify keyboard interaction, including opening and dismissing the interruption dialog.
- Keep fixture identities and the local learning route out of production runtime.

## Verification

- 35 .NET tests passed. They cover spelling, inflection and Leitner transitions, return timing, direction, result counting and duplicate submissions.
- PostgreSQL 18 integration passed with two identities, concurrent repeated submissions, process restart, full completion and saved partial progress.
- Development and production Angular builds and the public navigation test passed. The production build excludes the fixture route and its lazy component.
- Chrome verified the panel at 320, 375 and 1280 px. The keyboard flow covered a wrong choice, refresh of its feedback, refresh of the next question, dismissal of the interruption dialog and a 2-of-3 summary. Making a word due in the isolated database exercised typed input and a base-form retry through the UI.

## Report

The branch contains local implementation and verification. GitHub Actions and independent integration review are pending. The development browser advances with a manual action after feedback, and retrying an unconfirmed request requires the page to remain open. Real accounts, adaptive placement, offline continuation and staging are outside this increment.
