# Product overview

Verbulio gives Polish-speaking adults a few minutes of focused English vocabulary practice. The full public application also serves as a portfolio demo. The main path is landing page → 20-question adaptive placement test → result → account → first practice session. The placement label guides exercise selection. Learning uses both translation directions and alternates between four choices and typed answers based on the learner's history.

Guests can resume their server-owned placement test. A completed result can be claimed by an account once. Account holders access their own practice history, categories, settings, progress, streaks and account lifecycle. Transactional email covers confirmation and password reset. Operator tools manage dictionary imports, exports and retention.

Practice prioritizes due reviews, then unseen words at the user's level. A normal session targets ten distinct words and continues until each has been answered correctly. The first resolved answer per word governs the summary and the Leitner schedule, including retries and duplicate requests. A short category uses its actual number of available words. A paused or interrupted session does not increase the streak.

## First learning increment

Use a controlled bilingual fixture set and an authenticated test identity to exercise one full session through selection, evaluation, persistence and review scheduling. Verify both directions, typed and four-choice questions, a correction, an inflection retry and a wrong answer that returns. Verify the review table transitions and that a duplicate submission changes progress once. Confirm a refresh restores the same question and state, and that another identity cannot read the session. The public onboarding and account lifecycle are separate increments.

## Decisions for dependent increments

- The approved queue rule keeps unlimited returns. A wrong word goes after two available questions, after one if only one remains, or immediately if the queue is empty. A completed session still requires a correct answer for every selected word.
- Streak day boundary. Recommend storing the completion instant and the session's IANA time zone, preserving credited days after travel. A queued final answer should credit the captured completion day only after server acceptance, subject to a bounded clock policy.
- Duplicate sign-up email. Recommend a neutral registration response plus clear sign-in and reset links, with limits on email activity.
- [Placement scoring and bank requirements](placement.md). The experiment and engine cover synthetic response profiles. The guest flow and content validation are tracked separately.
- Dictionary. Check the rights and coverage of candidate sources before adapting 100–200 representative records. Keep provenance per entry and keep OpenAI out of user requests.

The owner-approved MVP checkout conditions cover full account and retention flows, interrupted connectivity, ownership enforcement, accessible responsive UI, operator import and a verified staging candidate. Educational effectiveness remains to be evaluated.


## Critical paths

| ID | Flow and actors | Why it is critical |
| --- | --- | --- |
| CP-001 | Guest starts or resumes placement, finishes 20 questions, creates an account and starts learning. [Placement](placement.md) | A new user needs an attainable starting point and an account linked to the server-owned result. |
| CP-002 | Account holder starts a session, answers and resumes, completes or ends early, then returns for due words. [Learning](learning.md) | Answer loss, duplicate progress or inaccessible practice defeats the daily learning goal. |
| CP-003 | Account holder confirms or recovers access and requests their own account data or deletion. | Access recovery, privacy and cross-account isolation protect personal data and saved progress. |

See the [verification plan](../qa/critical-paths.md).
