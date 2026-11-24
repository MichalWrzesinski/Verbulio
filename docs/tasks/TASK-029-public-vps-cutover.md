# TASK-029 Public VPS cutover

Status: BLOCKED

Serve the full Verbulio application at `https://verbulio.gitpush.app/` without an SSH tunnel. The owner authorized public availability. The current Nginx server block points at the withdrawn placeholder path `/srv/Verbulio`. The current application is packaged in fixture mode on loopback and uses an existing private preview database.

Prepare a public runtime and a separate PostgreSQL database for real accounts. Preserve the private preview environment. Keep application and database ports on loopback, and configure the public hostname to proxy through Nginx on HTTPS. Establish public account recovery, privacy information, retention and backup/restore verification before accepting public registrations. Prepare and verify an immutable CI deployment and an Nginx rollback. The one-time Nginx change requires VPS administration; the current operator account requires interactive sudo. All application files are transferred by GitHub Actions.
