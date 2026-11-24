# TASK-039 Password reset completion API

Status: DONE

Redeem the password reset token through an HTTPS POST with the existing local client guard and a per-route IP limit. Return 204 after the password change commits, without issuing or changing an account authentication cookie. Existing guest cookie behavior is independent. Record a successful reset without logging the token, password or account data. Report missing, expired and used tokens with one error code, and report a password policy failure separately without consuming the token.

Verify guards, error responses, one-time use, session revocation and log-in with the new password against isolated PostgreSQL. Token request delivery and browser screens are separate increments. The static public staging runtime exposes no account routes.
