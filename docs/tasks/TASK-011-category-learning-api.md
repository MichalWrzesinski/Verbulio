# TASK-011 Category learning API

Status: DONE

## Goal

Extend CP-002 with account-owned practice limited to a theme. This task provides the API for the separately implemented J-3 views.

## Acceptance

- List pilot categories only for authenticated accounts. Start category sessions through the existing account endpoint with an optional category ID.
- Apply the same level, due review and shared word progress rules within the chosen category. Report the actual size for short sessions and return the existing empty-pool error when no word is available.
- Preserve the single active session per account. Return its actual category on start, read, answer and end, including when a different category was requested. Existing sessions have no category.
- Verify API authorization, category selection through to completion, short results, and empty or unknown categories in the PostgreSQL check.

## Verification

The 55 .NET tests, Release build and isolated PostgreSQL integration passed. An A1 account completed the five distinct food items and received a result out of five. The integration also verified an empty category, an exhausted category, an unknown ID, resuming the same active session through a different category request and denied category listing for guests.

The pilot category assignments use existing catalog IDs. The J-3 views and presentation of availability are a subsequent client task.
