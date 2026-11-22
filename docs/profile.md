# Technical profile

AppStandards 5.0.0 (suite date 2026-11-15) was copied on 2026-11-22 from the default branch `main` of [AppStandards](https://github.com/MichalWrzesinski/AppStandards) at commit `410ba9cfdc8f3cab76ab712c98edcb42f9d169f9`. The full tracked snapshot is in [standards](standards/README.md).

The owner-approved stack is ASP.NET Core / C#, Angular / TypeScript and PostgreSQL. The frontend and backend choices demonstrate two additional ecosystems in the owner's portfolio. Use .NET 10 LTS for the backend and use the Angular workspace lockfile for the concrete frontend versions. The initial local frontend uses Node 26.13.0.

Use a modular monolith with server-owned placement results, sessions, review scheduling and authorization. API declarations in the backend will generate OpenAPI. Operator-only dictionary import runs separately from the web process. Public and portfolio traffic uses one application and one account model.

Before staging, confirm PostgreSQL persistence, a restore drill, scheduled retention and an approved transactional email transport. Use local capture for email development. Hosting, email delivery to real addresses and publication have separate prerequisites.

The learning slice uses .NET SDK 10.0.404, Npgsql and PostgreSQL 18 for local development and integration checks. Development cookie identities and their local data protection keys are isolated from the future account module.
