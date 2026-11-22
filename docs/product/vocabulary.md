# Pilot placement vocabulary

[Placement](placement.md) describes the selection and scoring contract. The local pilot is an original editorial EN–PL sample. Its source file is [placement-pilot-v1.tsv](../../api/Verbulio.Api/Dictionary/placement-pilot-v1.tsv). It contains 108 entries, 24 at A1, 20 at A2 and 16 each at B1, B2, C1 and C2. The labels are provisional difficulty buckets for testing the full path, with no learner calibration yet.

Each row contains one translation sense, a difficulty bucket and a four-item distractor group. A group has one level and alternatives of the same broad grammatical type. The loader uses the other three displayed forms as distractors for each direction. Optional columns describe inflections, accepted alternative translations and hints in the displayed language. Hints are rendered with the prompt and are excluded from accepted answers. In optional columns a hyphen means absent and pipes separate forms.

The sample was authored for Verbulio without importing external wordlists or dictionary records. Before growing the bank or making it public, review the entries and distractors with a bilingual editor, calibrate the provisional buckets, and record the applicable data license. Keep future external datasets identifiable by source and version with their own attribution and terms.

Version the embedded data as a unit. Do not edit the contents of a version referenced by stored tests once it has shipped. A changed bank receives a new version while the old one remains available for replay during its retention window.
