# TASK-029 Public VPS cutover

Status: BLOCKED

Serve the full Verbulio application at `https://verbulio.gitpush.app/` without an SSH tunnel. The owner authorized public availability. The first public site serves static content without registration or account APIs.

On 2026-11-24, the owner activated the root-owned Nginx include for revision `006104217683613de617ecad481825860eb2d917`. [GitHub Actions](https://github.com/MichalWrzesinski/Verbulio/actions/runs/45352941063) staged the exact release on loopback. External verification returned HTTPS 200 on the homepage and health endpoint, the expected security headers, and 404 for the registration endpoint.

Provision a separate PostgreSQL database before accepting real accounts, and retain the independent private preview. Establish public account recovery, privacy information, retention and backup/restore verification before enabling public registration. The public URL is staging only. [TASK-037](TASK-037-automatic-staging-deployment.md) tracks automatic single-instance staging updates after CI.
