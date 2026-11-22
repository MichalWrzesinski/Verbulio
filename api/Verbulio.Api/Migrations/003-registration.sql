ALTER TABLE guest_placement_tests
    ADD COLUMN IF NOT EXISTS claimed_at timestamptz
        CHECK (claimed_at IS NULL OR (result IS NOT NULL AND NOT superseded));

CREATE TABLE IF NOT EXISTS accounts (
    id uuid PRIMARY KEY,
    email text NOT NULL CHECK (char_length(email) BETWEEN 1 AND 254),
    normalized_email text NOT NULL UNIQUE,
    password_hash text NOT NULL CHECK (password_hash <> ''),
    starting_level integer NOT NULL CHECK (starting_level BETWEEN 0 AND 5),
    placement_test_id uuid NOT NULL UNIQUE REFERENCES guest_placement_tests(id),
    created_at timestamptz NOT NULL,
    email_confirmed_at timestamptz
);
