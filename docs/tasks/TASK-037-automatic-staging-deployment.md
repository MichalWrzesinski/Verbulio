# TASK-037 Automatic staging deployment

Status: DONE

Treat the public URL as staging. Deploy the current main revision automatically after its successful push CI. Accept only the repository's own main push runs, pin the exact attempt and artifact, and skip superseded revisions. Keep the existing single-instance replacement, ingress check and rollback. Private previews remain independent. No production environment or manual release approval is configured.
