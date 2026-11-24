# TASK-034 Production candidate verification

Status: DONE

Provide a read-only GitHub Actions workflow to verify the exact candidate presented for production approval. Check the commit, successful main CI attempt, immutable artifact ID, GitHub artifact digest, downloaded ZIP digest, and the archive revision. Compare the expected deployed Nginx fingerprint with the candidate's tracked ingress and report the archive SHA-256.

This workflow makes no server changes and its success is evidence for approval, not approval itself. Production promotion must compare the live configuration again under the deployment lock and require a human to approve the exact candidate before switching traffic.
