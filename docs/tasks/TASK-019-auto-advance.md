# TASK-019 Automatic advance in settings and practice

Status: DONE

Show the account's automatic advance preference in the J-4 learning settings. Save the choice through the authenticated preference API and report confirmed success, failed or uncertain saves, and session expiry. Reading back the setting resolves a failed save before another change.

For account practice with the preference enabled, advance about one second after a newly accepted answer graded Correct when another question exists. Wrong answers, spelling feedback, base-form retries, summaries and restored feedback remain manual. End-dialog entry, manual advance, leaving practice and signing out cancel pending advance. When the tab is hidden at the scheduled moment, advance remains manual. A failed preference read falls back to manual advance.

Verify Angular builds and tests, account preference round-trip after refresh, keyboard activation, disabled default and save recovery, automatic correct feedback, manual incorrect feedback and interruption cancellation in Chrome. This settings slice only covers the learning preference from J-4.
