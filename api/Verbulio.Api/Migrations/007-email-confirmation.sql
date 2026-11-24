ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS email_confirmation_digest bytea
        CHECK (octet_length(email_confirmation_digest) = 32),
    ADD COLUMN IF NOT EXISTS email_confirmation_expires_at timestamptz;

CREATE UNIQUE INDEX IF NOT EXISTS accounts_email_confirmation_digest
    ON accounts (email_confirmation_digest)
    WHERE email_confirmation_digest IS NOT NULL;
