# TASK-009 Local login form

Status: DONE

## Goal

Exercise CP-001 and CP-003 from a registered local account to a browser account session and sign-out. Reuse mockup K-01 and the existing HTTPS account API.

## Acceptance

- Guest landing and the registered-account confirmation link to login. Plain HTTP does not render a credential form.
- Invalid credentials are explained without disclosing whether the email exists. Successful sign-in shows the saved level, survives refresh and can be ended with logout.
- Compare K-01 with the implementation at 375 and 1280 px and verify 320 px, keyboard and relevant response/error states.
- Keep the K-01 fixture in the development build. Password reset and account learning are separate.


## Verification

CP-001 and CP-003, local browser session segment. The Angular development and production builds and both navigation tests passed. Chrome completed placement and registration, checked invalid credentials and secure cookie attributes, checked the active session after successful responses were lost, distinguished an existing session from an unconfirmed login attempt, and restored the signed-in state on refresh. Axe reported zero violations for the A/AA tags checked and no unexpected response statuses or application console warnings were observed.

Side-by-side comparison with K-01 at 375 and 1280 px confirmed the frame, form positions, typography, fields and button. The recovery action is pending and the heading has visible keyboard focus. The form has no horizontal overflow at 320 px.

## Report

The development login route requires HTTPS and the Angular proxy verifies the API certificate. The fixture shows the account level and permits sign-out. Account learning and password recovery remain separate.
