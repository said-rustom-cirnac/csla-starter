# CslaStarter

A line-of-business web application for the Government of Canada: .NET 10, Blazor Server, CSLA 10.2 and EF Core on PostgreSQL. Generated with `dotnet new csla-blazor`.

## Structure

- `src/CslaStarter.Domain`: CSLA business objects with their rules and permissions, and the DAL contracts and DTOs.
- `src/CslaStarter.Data`: EF Core and PostgreSQL, and the DAL implementations.
- `src/CslaStarter.Web`: the host, the components and pages, and the GC Design System in `wwwroot/gcds`.
- `tests/CslaStarter.Architecture.Tests`: checks of the project boundaries and conventions.

Each project has `Platform/` (the starter's code, the same in every app) and `Features/` (your app's own code). See `docs/handbook/architecture.md`.

## First steps

1. **Build and test:** `dotnet build -c Release`, then `dotnet test`. The other commands are in `docs/handbook/getting-started.md`.
2. **Run:** `dotnet run --project src/CslaStarter.Web`.
3. **GC Design System assets:** they ship in the web project (`src/CslaStarter.Web/wwwroot/gcds`). To move to a new version, change the pins in `scripts/update-gcds.sh`, run `bash scripts/update-gcds.sh` (needs Node.js), and commit what it writes, including `scripts/gcds.integrity`.
4. **Lock files:** `dotnet new` restored the packages and wrote `packages.lock.json` files. Commit them: CI restores in locked mode.

## Branches

If the app keeps the Claude workflow (`--claude`, the default), work goes to the `claude` branch through pull requests, and the `sync-main` workflow mirrors it to `main` without the Claude-only files. The first sync is manual, because the workflow only starts from `main`: follow the first-snapshot steps under "Sync to main" in `docs/claude/workflow.md`.
