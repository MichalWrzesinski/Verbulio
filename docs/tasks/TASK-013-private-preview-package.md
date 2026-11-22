# TASK-013 Private preview package

Status: DONE

## Goal

Package the current account and learning flow for private owner assessment on the existing VPS. Host setup and deployment require working SSH access and the server inventory.

## Acceptance and evidence

- The optimized preview includes the account routes. The public Angular build remains separate. The API accepts the explicit Preview environment and serves both the site and API via HTTPS on loopback. Preview responses carry a noindex header.
- CI builds a reproducible package identified by its checkout revision. The successful integrated run retains the package for staging.
- The extracted package passed local migration against isolated PostgreSQL and a Chrome run covering placement, registration, login, category availability, empty and short practice, resume and logout. Deep links returned the site, unknown API paths returned 404, and the process listened solely on IPv4 loopback.
- The [runbook](../operations/private-preview.md) specifies private access, configuration, isolated storage and how to withdraw the preview. Read-only SSH confirmed that the existing placeholder is separate and the preview port is free. The configured user cannot access the Docker daemon without interactive sudo. No server changes were made.
