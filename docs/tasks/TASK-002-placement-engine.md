# TASK-002 Adaptive placement engine

Status: IN_PROGRESS

## Goal and scope

Implement the scoring and question flow for the 20-question vocabulary placement test. Compare candidate estimators before choosing one. Use synthetic evidence for this increment. The guest identity, database adapter and onboarding screens will consume this engine.

## Acceptance

- Ask exactly 20 distinct, adaptively selected questions, with ten four-choice questions and ten typed questions. Include both translation directions and accept “Nie wiem.”
- Expose the next question and progress before completion. Reveal only the orientation result at completion. Treat “Nie wiem” as a wrong answer.
- Show a stable question and produce the same result when replaying the same pinned bank, seed and accepted submissions. Reject stale questions and conflicting submission keys.
- Estimate A1 after all wrong answers and at least C1 after all correct answers. Compare both orders of one late or early exception and document the assumptions and limits.
- Validate the input bank and explicitly reject inadequate coverage for the supported adaptive levels.


## Verification

CP-001. Exercise the engine and synthetic scenarios with .NET tests. The complete guest-to-account path depends on the bank, persistence and onboarding increments. Reuse existing learning tests to verify the shared distractor validation.

## Report

The 49 .NET tests passed and the API built in Release. The scoring comparison and replay of typed answers use a pinned synthetic bank. The guest route, a reviewed vocabulary bank, PostgreSQL persistence and result claims remain separate subsequent increments.
