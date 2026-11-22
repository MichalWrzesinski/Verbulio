CREATE TABLE IF NOT EXISTS account_sessions (
    id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    expires_at timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS account_sessions_expiration ON account_sessions (expires_at);
