# Private VPS preview

The private preview runs the placement, registration, login and practice fixtures from one HTTPS process. The optimized Angular build uses the same account routes as development. The public build and API production mode retain their current boundaries.

## Package

Use the `verbulio-preview` artifact from the successful CI run for the integrated commit. To reproduce it locally, use a clean checkout and the repository's Node and .NET versions. Run `npm ci` in `client/` and then run the following from the repository root.

```sh
bash scripts/build-preview.sh /tmp/verbulio-preview.tar.gz
```

Record the archive checksum before transfer and verify it after transfer. The archive contains the portable .NET app, the Angular site and a `REVISION` file with the checkout commit. Certificates, credentials and database data are supplied separately.

## Deploy from GitHub Actions

Use **Deploy private preview** from main with the ID of a successful CI push run on main. The workflow verifies the package revision, sends the package and Compose configuration over pinned SSH, migrates the preview database, starts the service and checks its HTTPS health endpoint. Use the same workflow with a prior compatible candidate if the app must be rolled back. Verify database restoration separately before reverting across an incompatible schema change.

Provision `/srv/Verbulio-preview` for the dedicated `deploy-verbulio` user. Give that account access to Docker as for the other application deploy accounts on the VPS. Install a dedicated restricted SSH public key for the account and store its private half as the Actions secret `PREVIEW_SSH_KEY`. This model gives the deploy key administrative-equivalent access to the Docker host. Keep it exclusive to this private repository and rotate it independently. The operator provisions the account once. All application files travel via GitHub Actions.

The preview uses a dedicated persistent PostgreSQL volume and persistent TLS and cookie keys. The application and database listen on host IPv4 loopback only. Keep both services off public listeners and the public reverse proxy. The preview uses test accounts and does not send email.

From a machine with authorized operator SSH access, forward the port and visit `https://127.0.0.1:5443/` with the preview certificate trusted.

```sh
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:5443:127.0.0.1:5443 webhome-vps
```

Complete placement, registration, login and a category session through the tunnel after a deployment. To withdraw the preview without dropping its data, stop the `verbulio-preview` app container using Docker or stop the Compose project without removing volumes.
