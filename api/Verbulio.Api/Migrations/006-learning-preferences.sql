ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS auto_advance boolean NOT NULL DEFAULT false;
