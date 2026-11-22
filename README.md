# Verbulio

Verbulio helps Polish-speaking adults practise English vocabulary through short, adaptive sessions and spaced reviews.

- [Product overview](docs/product/overview.md)
- [Design references](docs/design/README.md)
- [Technical profile](docs/profile.md)
- [Project instructions](AGENTS.md)

## Local frontend

Use the Node.js version in `client/.nvmrc`. From `client/`, run `npm ci` and `npm start`. The current frontend implements the public landing screen and its navigation targets as work-in-progress states.

Local verification uses `npm run build` and `npm test -- --watch=false`. A GitHub Actions workflow runs the same commands when a remote is configured.

## Local learning engine

From `api/`, use the .NET SDK declared in `global.json` and run `dotnet test Verbulio.Learning.Tests -c Release`. The library exercises session behavior with controlled words and times. See the [learning contract](docs/product/learning.md) for the implemented rules and the persistence boundary.

## Local learning session in the browser

Use a dedicated PostgreSQL database for this fixture. For example, start PostgreSQL with Docker on the loopback interface:

```sh
docker run --rm --name verbulio-local-db \
  -e POSTGRES_DB=verbulio -e POSTGRES_PASSWORD=verbulio-local-fixture \
  -p 127.0.0.1:55432:5432 postgres:18-alpine
```

Once PostgreSQL is ready, run from `api/`:

```sh
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5185
export ConnectionStrings__Learning='Host=127.0.0.1;Port=55432;Database=verbulio;Username=postgres;Password=verbulio-local-fixture'
dotnet run --project Verbulio.Api -- --migrate
dotnet run --project Verbulio.Api
```

From `client/`, run `npm start -- --host 127.0.0.1 --proxy-config proxy.conf.json` and open [the local learning session](http://127.0.0.1:4200/lokalna-nauka). The Angular route exists only in the development build and the API accepts only local development traffic. Use the same browser to resume the active session. The fixture establishes an isolated cookie identity with three dictionary entries. Use “Dalej” to advance after feedback.

To run the database integration check, set `ConnectionStrings__Learning` to an isolated test database, build the release configuration with `dotnet build Verbulio.Api -c Release`, then run `python3 checks/local_learning.py` from `api/`. The check starts and restarts its own local API.


The development landing page also opens the local placement test. The browser can resume or restart the saved test and display its server result after 20 questions. New local tests use the [editorial pilot bank](docs/product/vocabulary.md). The labels are provisional and the result remains marked as test data. After completion, use “Załóż konto” with a test address to create a local account with the saved level. The production build continues to use the placeholder for `/test`.
