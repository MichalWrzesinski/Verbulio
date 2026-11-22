# Verbulio

Verbulio helps Polish-speaking adults practise English vocabulary through short, adaptive sessions and spaced reviews.

- [Product overview](docs/product/overview.md)
- [Design references](docs/design/README.md)
- [Technical profile](docs/profile.md)
- [Project instructions](AGENTS.md)

## Local frontend

Use the Node.js version in `client/.nvmrc`. From `client/`, run `npm ci` and `npm start`. The current frontend implements the public landing screen and its navigation targets as work-in-progress states.

Local verification uses `npm run build` and `npm test -- --watch=false`. A GitHub Actions workflow runs the same commands when a remote is configured.
