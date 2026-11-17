# Getting started

The starter is being rebuilt, one slice at a time. Today the solution builds, and its architecture tests, domain unit tests and integration tests run; the integration tests use a PostgreSQL that Testcontainers starts in Docker. There are no pages and no sign-in yet.

## Prerequisites

- The .NET 10 SDK. `global.json` pins 10.0.100 and accepts later feature bands.
- Docker, for the integration tests (Testcontainers starts PostgreSQL) and for the local containers in `docker-compose.yml`.
- Bash, git and Python 3, for the script tests.
- Node.js, only to move the GC Design System to a new version.

## Commands

Run them from the repository root.

| Task | Command |
|---|---|
| Restore the local tools (`dotnet-ef`, CycloneDX) | `dotnet tool restore` |
| Build (warnings are errors) | `dotnet build -c Release` |
| Test (xUnit v3 on Microsoft Testing Platform) | `dotnet test` |
| One test project | `dotnet test --project tests/CslaStarter.Architecture.Tests` |
| One test | `dotnet test --project tests/CslaStarter.Architecture.Tests --filter-method "*TestName"` |
| Script tests | `bash scripts/tests/run.sh` |
| Format check, as CI runs it | `dotnet format --verify-no-changes` |
| Local PostgreSQL and mock Entra ID | `docker compose up -d` |
| Apply the migration to the local database | `CSLASTARTER_DESIGN_CONNECTION="<Owner string>" dotnet ef database update -p src/CslaStarter.Data -s src/CslaStarter.Data` |
| Regenerate the one migration, `Initial`, after a model change | `rm -r src/CslaStarter.Data/Migrations && CSLASTARTER_DESIGN_CONNECTION="Host=localhost;Database=cslastarter" dotnet ef migrations add Initial -p src/CslaStarter.Data -s src/CslaStarter.Data` |
| Run the app | `dotnet run --project src/CslaStarter.Web` |
| After adding or changing a package | `dotnet restore --force-evaluate`, then commit the changed `packages.lock.json` files |
| Move the GC Design System to a new version | Change the pins in `scripts/update-gcds.sh`, run `bash scripts/update-gcds.sh`, then review and commit what it writes |

CI restores with `--locked-mode`, so a package change without updated lock files fails the build.

The app answers only `/health/live` for now.

Next: [architecture](architecture.md).
