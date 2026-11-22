CREATE TABLE IF NOT EXISTS learning_days (
    owner_id uuid NOT NULL REFERENCES accounts(id),
    credited_day date NOT NULL,
    completed_at timestamptz NOT NULL,
    time_zone text NOT NULL,
    PRIMARY KEY (owner_id, credited_day)
);
