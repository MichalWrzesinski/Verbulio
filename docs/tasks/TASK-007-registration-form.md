# TASK-007 Local registration form

Status: DONE

## Goal

Exercise CP-001 from a completed test to a saved account in the local browser. Reuse J-1 registration layout and the server-owned result. Account login and real mail remain separate.

## Acceptance

- The completed result links to registration. Opening the form without a completed test sends the user back to take or resume it.
- Show the saved result and use the existing server API to register once. The request contains the test ID and credentials only. Render the starting level returned by the API.
- Preserve form fields on a recoverable failure, prevent repeated submission while saving, and clear the password after success or expired identity.
- Compare the form to J-1 at 375 and 1280 px and verify keyboard behavior and 320 px layout. Keep the fixture development-only.


## Verification

CP-001, local test-to-account segment. The Angular development and production builds and navigation test passed. Chrome checked the guest guard, completed 20 questions, and created an account with the server's level. A network failure before the request was sent preserved the entered fields and allowed a successful retry. Keyboard tests covered the form, password visibility and success focus. There was no horizontal overflow at 320, 375 and 1280 px. Axe returned no violations for the WCAG A/AA tags checked and Chrome recorded no application console warnings or unexpected response codes.

The comparison against J-1-07 at 375 and 1280 px confirmed the panel, typography, spacing, fields and button arrangement. The preview has a shorter form because real mail, bot protection and the production consent flow are pending. It uses the current server password rule and preserves a visible focus outline.

## Report

The development result screen links to the local form. Successful registration shows the stored level. Authentication and the first account learning session are later work, so the success screen links to the start.
