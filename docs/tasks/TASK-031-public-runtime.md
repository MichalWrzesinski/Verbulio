# TASK-031 Public runtime without registration

Status: DONE

Add a `Public` runtime for the public Angular build on `127.0.0.1:5444`. Serve the public site over the existing Nginx ingress and require its HTTPS forwarding and canonical hostname. Keep all fixture and account endpoints out of this runtime. It does not open a database connection. Provision a separate database before adding public account features.

Verify static and SPA routes, missing API routes, and rejection of requests without the expected host or trusted HTTPS forwarding. Confirm the private preview and development API checks still pass. Packaging, installation and the Nginx switch remain [cutover work](TASK-029-public-vps-cutover.md).
