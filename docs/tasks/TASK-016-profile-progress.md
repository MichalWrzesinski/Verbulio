# TASK-016 Profile vocabulary progress

Status: DONE

Show a signed-in learner their starting level and the three vocabulary progress counts from J-4. Use only the account's own tracked words in its starting level. New words have no progress row, mastered words are in the Mastered review box, and the remaining tracked words are in learning. The total is the current pilot catalog size for the level.

The authenticated HTTPS `GET /api/v1/local-account/profile` returns `startingLevel`, `newWords`, `learningWords` and `masteredWords`. Guests and signed-out users are denied. Renewal of the session is independent of the persisted counts.

The Angular profile opens from the signed-in page, uses the comic layout, offers retry after a load failure, and routes expired or unauthenticated sessions to a sign-in action. Streak and settings are separate increments.

Verification covers 55 .NET tests, PostgreSQL owner isolation using two A1 accounts and fresh/after-practice counts, Angular development and production builds and both Angular tests. Keyboard-driven Chrome exercised loading and retry, a new A1 profile, refreshed counts after 15 words and the signed-out state. The profile was checked at 320, 375 and 1280 px. Axe found no violations in the rules checked. A separate isolated database fixture verified an A1 word counted as mastered and an owned C2 row excluded from the A1 totals.
