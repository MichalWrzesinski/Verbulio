# TASK-036 Single-instance production deployment

Status: DONE

Keep one public container on loopback port 5444 and leave Nginx pointing at it. The owner accepts temporary downtime during replacement. Retain the release revision header and remove the alternate port. Under the deployment lock verify the live ingress and archive, replace the container, check the exact revision through public HTTPS, and restore the previous release on failure.

The owner starts Deploy production manually from main after checking the exact candidate. The workflow verifies the successful CI attempt, immutable artifact and ingress fingerprint. The public release contains no accounts or database migrations. Preview deployment remains separate.

[TASK-037](TASK-037-automatic-staging-deployment.md) supersedes the production classification and manual dispatch. The single-instance replacement and rollback are reused.
