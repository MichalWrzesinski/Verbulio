# TASK-012 Category views

Status: DONE

## Goal

Add the J-3 category flow to account practice on CP-002. Reuse the comic layout and the existing learning session.

## Acceptance

- Give the signed-in practice entry a quiet category action. Show the real number of currently eligible words per category, with distinct loading, error, empty and short-session states.
- Start from the chosen category, unless the server resumes an existing session. Display the actual category on its questions and summary.
- Offer general practice from the empty view and a fresh category list from a category summary. Keep practice and summary restorable after refresh.

## Verification

The 55 .NET tests, Release build and PostgreSQL integration passed. The category count for Jedzenie changed from five to zero after completion. Angular development and production builds and both tests passed.

Keyboard-driven Chrome opened the list, visited empty Emocje, completed Jedzenie with a result of five out of five, refreshed during the session, and checked the updated list. The existing general practice and logout regression also passed. Category views passed the checked Axe rules with no application warnings. The list had no horizontal overflow at 320, 375 and 1280 px. Compared against J-3 at mobile and desktop widths. The views show live availability and use the whole list for the next category, as the pilot may have no suitable recommendation.
