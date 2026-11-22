CREATE TABLE IF NOT EXISTS learning_sessions (
    id uuid PRIMARY KEY,
    owner_id uuid NOT NULL,
    active boolean NOT NULL,
    state jsonb NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS learning_sessions_one_active
    ON learning_sessions(owner_id) WHERE active;

CREATE TABLE IF NOT EXISTS word_progress (
    owner_id uuid NOT NULL,
    word_id text NOT NULL,
    direction integer NOT NULL CHECK (direction IN (0, 1)),
    mode integer NOT NULL CHECK (mode IN (0, 1)),
    review_box integer NOT NULL CHECK (review_box BETWEEN 1 AND 6),
    due_at timestamptz NOT NULL,
    PRIMARY KEY (owner_id, word_id)
);
