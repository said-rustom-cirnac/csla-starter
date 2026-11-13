# CslaStarter

A secure, bilingual line-of-business web application: .NET 10, Blazor Server, CSLA 10.2, EF Core on PostgreSQL, Entra ID sign-in, property- and record-level permissions, a full audit trail with change history, live updates between users, and English and French throughout, on a Protected B security baseline. Generated from the CSLA Starter template.

## First steps

1. **Local database and mock Entra ID:** `docker compose up -d` (PostgreSQL 18 and mock-oauth2-server), then apply the migrations:
   `CSLASTARTER_DESIGN_CONNECTION="<Owner connection string>" dotnet ef database update -p src/CslaStarter.Data -s src/CslaStarter.Data`
2. **GC Design System assets:** `scripts/update-gcds.sh` fetches the pinned packages into the component library. Commit what it writes, including `scripts/gcds.integrity`.
3. **Run:** `dotnet run --project src/CslaStarter.Web`. Sign in through the mock Entra ID, then make yourself the first administrator:
   `dotnet src/CslaStarter.Web/bin/Debug/net10.0/CslaStarter.Web.dll bootstrap-admin --tenant <tid> --object-id <oid>`
4. **Tests:** `dotnet test` (Docker needed for the integration tests). The end-to-end tests run with `E2E=1` once Playwright's browsers are installed (see `tests/CslaStarter.E2E.Tests`).
5. **Lock files:** `dotnet new` restored the packages and wrote `packages.lock.json` files. Commit them: CI restores in locked mode.

## Branches

If the app keeps the Claude workflow (`--claude`, the default), work goes to the `claude` branch through pull requests, and the `sync-main` workflow mirrors it to `main` without the Claude-only files. The first sync is manual, because the workflow only starts from `main`: follow "First sync" in `docs/claude/workflow.md`.

## Sample feature

Projects (with tasks and members) demonstrate every pattern. Remove them with `docs/claude/recipes/remove-sample.md` once you have features of your own.
