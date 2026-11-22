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
- Placement. Compare difficulty and result estimators against synthetic profiles before selecting an algorithm. Require all-wrong to produce A1 and all-correct at least C1. Treat the result as an exercise-selection estimate.
- Dictionary. Check the rights and coverage of candidate sources before adapting 100–200 representative records. Keep provenance per entry and keep OpenAI out of user requests.

The owner-approved MVP checkout conditions cover full account and retention flows, interrupted connectivity, ownership enforcement, accessible responsive UI, operator import and a verified staging candidate. Educational effectiveness remains to be evaluated.
