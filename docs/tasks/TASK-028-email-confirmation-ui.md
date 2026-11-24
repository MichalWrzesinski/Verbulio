# TASK-028 Email confirmation screen

Status: DONE

Consume the confirmation link on a preview route. Remove the fragment from browser history, hold its token only in the current screen instance and submit it after an explicit action. Show the K-09 success state only after HTTP 204. Provide feedback for an invalid link or an unconfirmed outcome. The completion screen returns to sign-in so links can be used on a different device.

This screen does not turn on Brevo or enforce the learning deadline.
