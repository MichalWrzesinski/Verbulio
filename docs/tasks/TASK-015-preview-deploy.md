# TASK-015 Preview deployment through GitHub Actions

Status: DONE

Replace the transfer-only workflow with the established dedicated SSH deploy account model. Validate main CI provenance, transfer the package and service files exclusively from GitHub Actions, and activate and verify the HTTPS preview on loopback with isolated PostgreSQL.

Workflow parsing, shell lint and Compose validation passed. An isolated Linux test with stubbed Docker and HTTPS commands verified unpacking, deterministic retries, restoration after a failed candidate and certificate renewal. A first live run requires the VPS operator to authorize the key and grant the dedicated deploy user Docker access. The public domain remains separate.
