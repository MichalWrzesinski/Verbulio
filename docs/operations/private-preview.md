# Private VPS preview

The private preview runs the placement, registration, login and practice fixtures from one HTTPS process. The optimized Angular build uses the same account routes as development. The public build and API production mode retain their current boundaries.

## Package

Use the `verbulio-preview` artifact from the successful CI run for the integrated commit. To reproduce it locally, use a clean checkout and the repository's Node and .NET versions. Run `npm ci` in `client/` and then run the following from the repository root.

```sh
bash scripts/build-preview.sh /tmp/verbulio-preview.tar.gz
```

Record the archive checksum before transfer and verify it after transfer. The archive contains the portable .NET app, the Angular site and a `REVISION` file with the checkout commit. Certificates, credentials and database data are supplied separately.

## Preview host

The package requires an ASP.NET Core 10 runtime, a dedicated PostgreSQL database and user, an HTTPS certificate valid for 127.0.0.1 and a persistent directory for the preview's key ring. Use a restrictive service user and an isolated installation directory. Preserve the existing placeholder and its installation.

Set the following environment values outside the release directory using a service configuration readable only by the operator and the service. In commands that start the app, use the extracted release as the working directory.

| Setting | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Preview` |
| `ConnectionStrings__Learning` | Connection string for the dedicated preview database |
| `LearningKeyDirectory` | Absolute preview-only persistent key directory |
| `ASPNETCORE_Kestrel__Certificates__Default__Path` | Path to the preview TLS certificate |
| `ASPNETCORE_Kestrel__Certificates__Default__KeyPath` | Path to the private key for a PEM certificate |

Back up and verify restoration of the isolated database before updating a preview with data. Run `dotnet Verbulio.Api.dll --migrate`, then `dotnet Verbulio.Api.dll` with that configuration. The preview listens on the VPS IPv4 loopback on port 5443. Keep it off the public reverse proxy and firewall. The fixture accepts test accounts and has no transactional email delivery.

From a machine with authorized SSH access, forward the port and visit `https://127.0.0.1:5443/` with the preview certificate trusted.

```sh
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:5443:127.0.0.1:5443 webhome-vps
```

Check the health endpoint through the tunnel and complete placement, registration, login and a category session with test data. Revert by stopping the preview service and selecting the previous release, restoring the isolated database if the candidate changed the schema incompatibly. For the first preview, stopping the service withdraws access. Keep the public virtual host separate.
