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
| Apply the migration to the local database (after `docker compose up -d`) | `CSLASTARTER_DESIGN_CONNECTION="Host=localhost;Database=cslastarter;Username=app_owner;Password=owner-dev-only" dotnet ef database update -p src/CslaStarter.Data -s src/CslaStarter.Data` |
| Regenerate the one migration, `Initial`, after a model change | `rm -r src/CslaStarter.Data/Migrations && CSLASTARTER_DESIGN_CONNECTION="Host=localhost;Database=cslastarter" dotnet ef migrations add Initial -p src/CslaStarter.Data -s src/CslaStarter.Data` |
| Run the app | `dotnet run --project src/CslaStarter.Web` |
| After adding or changing a package | `dotnet restore --force-evaluate`, then commit the changed `packages.lock.json` files |
| Move the GC Design System to a new version | Change the pins in `scripts/update-gcds.sh`, run `bash scripts/update-gcds.sh`, then review and commit what it writes |

CI restores with `--locked-mode`, so a package change without updated lock files fails the build.

## The local database

Migrations run as `app_owner`, the role that owns the database. The local PostgreSQL from `docker-compose.yml` has a database `cslastarter` owned by `app_owner`. Both are created the first time its data volume is created, by `deploy/docker/initdb/10-create-roles.sh`, which runs `deploy/sql/create-roles.sql`. The local-only passwords are set in `docker-compose.yml`: `APP_OWNER_PASSWORD` (`owner-dev-only`, used in the command above), `APP_RUNTIME_PASSWORD` and `POSTGRES_PASSWORD`. Any other environment has its own owner connection string, which is never kept in the repository.

The app answers only `/health/live` for now.

Next: [architecture](architecture.md).
