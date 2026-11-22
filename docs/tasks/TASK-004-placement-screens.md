# TASK-004 Local placement screens

Status: IN_PROGRESS

## Goal

Exercise CP-001 from landing through the displayed placement result in Angular with the local ASP.NET API and PostgreSQL. Reuse J-1 HTML, design tokens and portrait assets. Use synthetic data with an explicit local label. Account registration remains outside this increment.

## Acceptance

- The landing action opens the test and a completed run contains exactly 20 questions, with choices, typed input and skip. Show no correctness feedback during the test.
- On return or refresh, offer resume and restart for a started test. A completed result survives refresh.
- Preserve the J-1 shell and verify responsive layout at 320, 375 and 1280 px. Use keyboard-operable controls and focus the new question, resume heading or result.
- Prevent double submission while requesting an answer. Offer a retry with the same submission key after an uncertain network response and reload the current question after a conflict.
- Keep the synthetic test development-only. Include explicit screen and result wording describing the local fixture.

## Verification

CP-001, local browser segment. Angular development and production builds and the navigation test passed. Chrome checked the choice, typed, resume and result layouts at 320, 375 and 1280 px without horizontal overflow. The keyboard scenario submitted a choice and a typed response, skipped questions, completed all 20, and confirmed the saved result after refresh. It also checked resume before and after the first answer, explicit restart, retry after a response lost after server commit, recovery after a competing tab advanced the test, and starting a new test after the fixture cookie expired.

Side-by-side comparison at 375 and 1280 px used J-1 views 02, 03, 05 and 06. The same comic frame, portrait, controls, progress and centered resume/result panels are used. Visible differences are the fixture notice, the static reference's prefilled answer, live progress, fixture result wording and keyboard focus rings. Chrome captured no application console warnings or unexpected response statuses. Axe-core 4.10.3 reported zero violations for the WCAG A/AA tags checked on these four screens. Keyboard operation was exercised manually by scripted key events. A full assistive-technology audit is pending.

## Report

The development build maps the landing action to the local fixture. The tab stores only the opened test identifier to distinguish first entry from a refresh before an answer. Prompts and estimates are explicitly marked test data. Registration and the reviewed dictionary are separate work. The production build excludes the fixture component.
