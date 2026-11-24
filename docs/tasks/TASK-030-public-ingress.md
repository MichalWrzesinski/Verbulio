# TASK-030 Public HTTPS ingress configuration

Status: DONE

Prepare the Nginx location for the existing TLS virtual host at `verbulio.gitpush.app`. Proxy to the planned public runtime on `127.0.0.1:5444`, which was free during the VPS inspection. Private preview uses `127.0.0.1:5443`. The public request host and scheme are fixed by the TLS virtual host, and the proxy replaces forwarded identity headers with its own client address and scheme.

Keep the existing TLS and HTTP redirect blocks. The future public runtime must bind exclusively to loopback, use its own database, and trust forwarded client address and scheme only from the Nginx loopback peer. Use Nginx syntax validation and verify that the backend receives proxy-controlled forwarding headers before activation. [TASK-029](TASK-029-public-vps-cutover.md) tracks activation and restoration of the previous location if cutover fails.
