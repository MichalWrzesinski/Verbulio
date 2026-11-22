# Adaptive vocabulary placement

The test starts at A2. A correct answer raises the next question's target one level and an incorrect answer lowers it one level. Clamp to A1–C2. A versioned bank must offer enough distinct questions for the longest possible run at a single level. Alternate between four choices and typed answers. Cycle the translation directions so each mode appears five times in each direction across 20 questions.

## Scoring comparison

Both candidates used the same adaptive question path, starting level and alternating modes. The last-step candidate reports the next question's level after answer 20. The likelihood candidate chooses the level whose response probabilities best fit all 20 observations, using the shown difficulty and answer mode. The comparison uses synthetic correct and wrong responses.

| Synthetic response profile | Last step | Likelihood |
| --- | --- | --- |
| All wrong | A1 | A1 |
| All correct | C2 | C2 |
| 19 correct, then one wrong | C1 | C2 |
| One wrong, then 19 correct | C2 | C2 |
| 19 wrong, then one correct | A2 | A1 |
| One correct, then 19 wrong | A1 | A1 |
| Alternate starting with correct | A2 | A2 |
| Alternate starting with wrong | A2 | A1 |

Use the likelihood candidate for the engine. A late exception does not outweigh the preceding 19 answers in the tested cases. The modeled probability is a logistic curve with slope 1.4 per level, intercept 0.5 at equal difficulty, a ceiling of 0.98, and a floor of 0.25 for four choices or 0.02 for typed answers. Sum log probabilities for correct or incorrect responses. Assume an equal prior for each level and resolve ties toward the lower level. These parameters are engineering assumptions, independent of the CEFR descriptors. The two alternating sequences have different item difficulties and modes, so they can produce different estimates.

The estimate is for choosing initial vocabulary exercises. Results will require a representative, reviewed bank, calibration on real responses and monitoring of mode and item difficulty. Synthetic profiles establish boundary and stability behavior. They provide no evidence that the estimate measures language proficiency.

## Input and integration

The engine expects a pinned bank with unique IDs, at least 19, 10, 10, 9, 9 and 16 bilingual items respectively for A1 through C2, reviewed meanings and valid choices in both directions. The bank version also pins the broader dictionary forms used to reject spelling collisions. A change to either the prompts or those forms requires a new bank version. It accepts form-matched translations and tolerated spelling corrections through the learning evaluator. A recognized inflection during placement counts as wrong when it differs from all accepted forms. This keeps scoring to one response per question and avoids revealing hints.

Question responses contain only the displayed prompt, choices, direction, mode and sequence number. Internal grades and the modeled probabilities stay on the server. An identical submission retry returns the current test state. A revision to a stored submission or an answer for an inactive question is rejected. The guest adapter must persist the initial seed, pinned bank version, active question and accepted submissions, and enforce guest ownership before returning any state. Persist the final result transactionally and allow a single claim by the registering account. Historical engine versions must remain replayable or be migrated with their active question and grading data intact.


The development adapter keeps one current test per local fixture identity. It atomically supersedes the old test when the guest explicitly restarts. Store the bank version, shuffle seed, at most 20 submissions and the authoritative result in PostgreSQL. This slice runs against synthetic prompts on the loopback interface. The public guest cookie, retention, and registration transaction that claims the result are still required for CP-001.
