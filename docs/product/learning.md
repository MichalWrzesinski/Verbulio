# Learning session contract

The [overview](overview.md) defines acceptance for the end-to-end increment. The learning engine implements the rules below on input already scoped to a user's level or chosen category.

| Behavior | Rule |
| --- | --- |
| Selection | Due reviews sort by box, due instant and ordinal word ID. Mastered reviews come last among due reviews. Fill up to ten distinct words with reproducibly shuffled unseen words. The caller handles an empty selection. |
| Presentation | The first appearance of a new word is EN→PL with four choices. Alternate direction on every resolved appearance. A correct answer schedules typed input on the next appearance. A wrong answer schedules choices and returns the word according to the queue rule in the overview. |
| Accepted input | Compare NFC Unicode, invariant case and collapsed whitespace. Preserve punctuation under normalization. Match only a translation belonging to the target sense. |
| Spelling | Accept omitted Polish accents, including partial omission, and show the original spelling. Accept one insertion, deletion, substitution or adjacent transposition for inputs of at least four letters. An edit that produces another known form or also omits Polish accents receives no correction. |
| Inflection | Offer one retry on a recognized inflection. Keep the same prompt and require a new submission for the retry. A second inflection resolves as incorrect. |
| Choices | Require one accepted translation and three distinct distractors. Reject any distractor matching an accepted translation or its permitted accent omissions. The caller supplies translations and distractors from the pinned dictionary version for this session. |
| Progress | Store the direction and mode for the next appearance. Only the first resolved answer per word changes its review schedule or the first-try count. Reviews add 1, 3, 7, 14, 30 or 90 elapsed days to the server's UTC answer instant. |
| Completion | Resolve every selected word correctly. Ending early returns a partial summary. The denominator is the number of selected words, including when a category has fewer than ten. |

## Storage and API boundary

The local ASP.NET API uses PostgreSQL. HTTPS account practice uses the account scheme and takes its starting level from the stored account. New items come from that level of the versioned pilot bank. Previously tracked items can enter when due. The separate guest fixture retains its three items. Each identity can have at most one active learning session. It stores a dictionary version, selected words, starting progress, shuffle seed and accepted submissions. Replaying the submissions restores question number and choice order on the same engine and fixture versions. Before supporting database reuse across deployments, persist the actual choice order or provide a versioned replay implementation for in-flight sessions.

Authenticated HTTPS clients can read `GET /api/v1/local-account/learning/categories`. Each item has `id`, `title` and `availableWords`, the current count of due reviews and unseen words at the account's starting level within that category. Availability can change before a session starts.

`POST /api/v1/local-account/learning/sessions` accepts an optional `category` query parameter using the category ID. A valid request resumes any existing active session for the account before selecting new words. New category sessions use the shared word progress and are capped at ten words. An unknown ID returns `404 category_not_found`. With no active session and no eligible words, the endpoint returns `409 no_words_available`. Session views expose `category` and `categoryTitle` for the actual session. Both fields are null for general sessions and sessions stored before categories.

Every read and write scopes by the authenticated owner. An answer must target the current question and carry a unique submission key. The store serializes writes to one session and records the submission, updated session and progress in the same PostgreSQL transaction. An identical retry returns the prior feedback and the current session view. A reused key with a different payload yields `422`. An obsolete question yields `409`. Failed ownership checks return `404`. The answer timestamp comes from the server.

The Angular development route renders the comic learning layouts. It advances manually after feedback and can retry the same request after a failed network round trip while the page is open. Offline continuation across reloads, auto-advance and the account lifecycle are separate work. Account sessions resume from server state after sign-out and sign-in. The browser clears its practice checkpoint on sign-out. Unseen words begin with choices. Typed answers are used after a successful appearance when a word next becomes due.

## Evidence

- Unit tests cover the Leitner transition table, answer normalization and exception cases, queue fallback and deterministic replay.
- The [PostgreSQL check](../../api/checks/local_learning.py) opens two test identities, denies cross-identity reads and writes, submits the same answer concurrently, restarts the server, safely retries the first answer and checks complete and partial summaries.
