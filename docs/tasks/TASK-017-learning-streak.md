# TASK-017 Learning streak

Status: DONE

Show the current streak and lifetime record in the J-4 profile. Starting or ending early does not credit a day. The server credits the learner's local day once per account in the same transaction as the first accepted answer that completes a session.

New sessions pin the IANA time zone sent by the browser at start. An active legacy session uses the zone of the accepted completion request. Unsupported or absent time zones use UTC. On a timezone change, credited dates stay fixed and the profile evaluates current activity using the browser's present local date. An already credited future local date can contribute to the record and cannot keep the current streak alive. At least one completion today or yesterday keeps the run active. Daylight saving changes use the offset at the server's acceptance instant. Until offline synchronization is implemented, a final answer accepted after midnight counts on the new day in the pinned zone.

Existing completed sessions have no captured time zone and are not backfilled. The additive migration creates `learning_days`. The profile response adds `streak.currentDays` and `streak.bestDays`. Session start, answer and profile accept an optional `timeZone` query parameter.

Verification covers .NET calendar and daylight-saving tests, owner separation, pinned time zone across a change and same-day deduplication in PostgreSQL, partial ending, both Angular builds and navigation tests, plus Chrome checks of zero and one-day profiles.
