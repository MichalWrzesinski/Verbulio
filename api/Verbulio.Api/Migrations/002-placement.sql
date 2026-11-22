CREATE TABLE IF NOT EXISTS guest_placement_tests (
    id uuid PRIMARY KEY,
    guest_id uuid NOT NULL,
    bank_version text NOT NULL,
    seed integer NOT NULL,
    submissions jsonb NOT NULL DEFAULT '[]'::jsonb
        CHECK (jsonb_typeof(submissions) = 'array' AND jsonb_array_length(submissions) <= 20),
    result integer CHECK (result BETWEEN 0 AND 5),
    superseded boolean NOT NULL DEFAULT false,
    CHECK ((result IS NOT NULL) = (jsonb_array_length(submissions) = 20))
);
CREATE UNIQUE INDEX IF NOT EXISTS guest_placement_current
    ON guest_placement_tests (guest_id) WHERE NOT superseded;
