# TASK-038 Password reset storage

Status: DONE

Generate a single-use password reset token valid for 30 minutes and persist only its digest. Enforce an atomic one-minute issuance interval per account. Reset uses the registration password policy and updates the password and revokes existing browser sessions in one transaction. Login revalidates the credential snapshot under the same account lock so an old password verified before reset cannot create a session afterward.

Verify rotation, exact expiry, single use under competing submissions, old and new credentials, session revocation and owner isolation in PostgreSQL. Delivery and the recovery API and screens are separate increments.
