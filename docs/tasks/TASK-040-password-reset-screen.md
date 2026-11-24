# TASK-040 Password reset completion screen

Status: DONE

Build the HTTPS preview page for the K-06 to K-08 reset states. Remove the reset token from browser and router history, submit only after the user enters a valid new password, and confirm only after HTTP 204. Clear the password input on submission and link replacement. Show a distinct invalid-link state, and allow another attempt when the result is unconfirmed.

The request-email flow is separate. Until it is available the invalid link returns to sign-in. The success state shows the K-07 notice and links to the existing sign-in form without transferring the password or an email address. Public staging exposes no account routes.
