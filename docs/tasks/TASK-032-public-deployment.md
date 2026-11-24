# TASK-032 Public deployment through GitHub Actions

Status: DONE

Build the production Angular frontend and package it with the isolated public runtime. Deploy the verified CI artifact with the existing GitHub Actions SSH identity for the same VPS account. Store releases under the non-web-accessible `/srv/Verbulio/.runtime` directory and run a separately named container bound to port 5444. Promote a release only after its loopback health check passes and attempt restoration of the previous release on failure.

The workflow stages the Nginx location configuration alongside the service. [TASK-029](TASK-029-public-vps-cutover.md) tracks privileged virtual-host activation and external verification.
