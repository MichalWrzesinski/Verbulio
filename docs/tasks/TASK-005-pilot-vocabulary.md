# TASK-005 Pilot vocabulary bank

Status: DONE

## Goal

Exercise CP-001 with an editorially authored EN–PL bank in the local development flow. Give new tests 100–200 real entries and preserve replay of the synthetic bank for already started tests.

## Acceptance

- Supply real words and translations across all placement levels, including Polish accents, sense hints, inflections and accepted variants.
- Validate four distinct choices in both directions for every entry through the existing placement engine.
- Use the pilot for new local tests and still resolve the synthetic snapshot by its pinned version. Keep experimental level labels clearly marked in the UI.
- Exercise the PostgreSQL placement flow through 20 questions with the pilot bank. Review language quality and make no calibration claim from fixture evidence.


## Verification

CP-001, local placement segment. 52 .NET tests, Release API compilation and the isolated PostgreSQL integration passed. The added checks validate all 108 items through the placement engine, exercise a sense hint and inflection, and confirm the synthetic version can still replay. Chrome completed 20 questions using the pilot, including typed input, restart, resume and persisted result. The public account portion and validation with real learners remain pending.

## Report

The fixture now uses an original, versioned EN–PL bank for new tests. The sample uses provisional difficulty buckets and is for local evaluation. The integration leaves stored synthetic tests attached to their original bank version.
