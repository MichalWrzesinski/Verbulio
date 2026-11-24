# TASK-029 Public VPS cutover

Status: BLOCKED

Serve the full Verbulio application at `https://verbulio.gitpush.app/` without an SSH tunnel. The owner authorized public availability. The current Nginx server block points at `/srv/Verbulio`, which the owner recreated empty and assigned to `deploy-verbulio`. The domain returns HTTP 403 while the directory is empty. The current application is packaged in fixture mode on loopback and uses an existing private preview database.

Use the [public runtime](TASK-031-public-runtime.md) for the initial site. Provision a separate PostgreSQL database before accepting real accounts. Preserve the private preview environment. Keep application and database ports on loopback, and configure the public hostname to proxy through [the HTTPS ingress](TASK-030-public-ingress.md). Establish public account recovery, privacy information, retention and backup/restore verification before accepting public registrations. Prepare and verify an immutable CI deployment and an Nginx rollback. The one-time Nginx change requires VPS administration; the current operator account requires interactive sudo. All application files are transferred by GitHub Actions.
