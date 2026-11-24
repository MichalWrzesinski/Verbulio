# Verbulio project instructions

Read [core](docs/standards/core.md), [routing](docs/standards/routing.md), and [workflow](docs/standards/workflow.md). The vendored suite and agreed departures are recorded in the [technical profile](docs/profile.md).

Use C# / ASP.NET Core, Angular / TypeScript and PostgreSQL. This owner-approved stack supports portfolio breadth and overrides AppStandards defaults. Operator scripts cover administration. Project documentation is in English and the application UI is in Polish.

Start with the [product overview](docs/product/overview.md) and [design references](docs/design/README.md). Historical mockups are visual references. Apply current product rules where the historical flows differ.

Use the commands in [README.md](README.md) to start the frontend. From `client/`, use `npm run build` for a production compilation and `npm test -- --watch=false` for frontend tests.

The private GitHub Free repository uses an owner-approved manual integration gate. Integrate through a merge commit in a pull request only after checking the exact candidate against the current base, successful GitHub Actions frontend and backend jobs, and an independent review of that candidate. Recheck the gates when the head or relevant base changes. GitHub does not enforce this gate on the current plan.

Verbulio has staging only. The public staging URL is `https://verbulio.gitpush.app/`. GitHub Actions deploys the current main revision automatically after successful CI to one instance on port 5444. The owner accepts a brief interruption during replacement. The private preview is a separate test environment. No production deployment is configured.
