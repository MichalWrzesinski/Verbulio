ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS password_reset_digest bytea
        CHECK (octet_length(password_reset_digest) = 32),
    ADD COLUMN IF NOT EXISTS password_reset_expires_at timestamptz;

CREATE UNIQUE INDEX IF NOT EXISTS accounts_password_reset_digest
    ON accounts (password_reset_digest)
    WHERE password_reset_digest IS NOT NULL;
