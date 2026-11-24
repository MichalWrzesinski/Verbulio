# TASK-032 Public deployment through GitHub Actions

Status: DONE

Build the production Angular frontend and package it with the isolated public runtime. Deploy the verified, attempt-specific CI artifact by its immutable artifact ID with the existing GitHub Actions SSH identity for the same VPS account. Store releases under the non-web-accessible `/srv/Verbulio/.runtime` directory and run a separately named container bound to port 5444. Record the pinned runtime and staged Nginx location together with each release. Switch one current-release pointer only after its loopback health check passes and attempt restoration of the previous release on failure.

This workflow stages releases before the first public cutover. It refuses to modify the service once the root-owned `/etc/nginx/snippets/verbulio-public.conf` exists. Later public releases require a separate production approval mechanism. [TASK-029](TASK-029-public-vps-cutover.md) tracks privileged virtual-host activation and external verification.

[TASK-036](TASK-036-single-instance-production.md) supersedes this pre-cutover workflow for subsequent releases.
