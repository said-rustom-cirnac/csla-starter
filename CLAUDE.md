# CSLA Starter

Jira project epic: DEV-37 (CSLA-starter)

## What this is

A starter for Government of Canada line-of-business web apps: .NET 10 Blazor Server, CSLA 10.2 business objects, EF Core with PostgreSQL, Entra ID sign-in, English and French, and a Protected B security baseline. It has three projects: `Domain`, `Data` and `Web`. Business rules and permissions live in CSLA objects; reusable `Csla*` components display them without repeating them. Interface text and error messages come from database lookups, not resource files.

The starter is being rebuilt: features arrive slice by slice, and only what the handbook describes exists today.

## Commands

| Task | Command |
|---|---|
| Build (warnings are errors) | `dotnet build -c Release` |
| Test (xUnit v3 on Microsoft Testing Platform) | `dotnet test` |
| One test project | `dotnet test --project tests/CslaStarter.Architecture.Tests` |
| Script tests (sync, guard, coverage check, template) | `bash scripts/tests/run.sh` |
| Format check, as CI runs it | `dotnet format --verify-no-changes` |
| Local PostgreSQL + mock Entra ID | `docker compose up -d` |
| Apply migrations locally | `CSLASTARTER_DESIGN_CONNECTION="<Owner string>" dotnet ef database update -p src/CslaStarter.Data -s src/CslaStarter.Data` |
| Run the app | `dotnet run --project src/CslaStarter.Web` |
| After adding or changing a package | `dotnet restore --force-evaluate`, then commit the changed `packages.lock.json` files |

CI restores in locked mode, so a package change without updated lock files fails the build. Integration tests need Docker (Testcontainers starts PostgreSQL).

## Architecture map

```
Domain → (none)        CSLA business objects, rules, DAL contracts and DTOs, the translator
Data   → Domain        EF Core and PostgreSQL, unit of work, audit, DAL implementations (never references CSLA)
Web    → Domain, Data  the host, the Csla* component kit, pages
```

Each project has `Platform/` (starter-owned, the same in every app) and `Features/<Feature>/` (the app's own code). `Platform` never references `Features`. A feature may use `Platform`, but never another feature's data. `CslaStarter.Architecture.Tests` fails if a reference or a boundary is broken.

- [CslaStarter.Domain](src/CslaStarter.Domain/CLAUDE.md): business objects, rules, permissions, DAL contracts.
- [CslaStarter.Data](src/CslaStarter.Data/CLAUDE.md): EF Core, PostgreSQL and the DAL implementations.
- [CslaStarter.Web](src/CslaStarter.Web/CLAUDE.md): the host, the component kit, the pages and the design system.
- [CslaStarter.Architecture.Tests](tests/CslaStarter.Architecture.Tests/CLAUDE.md): boundary, convention and documentation checks.

## Golden rules

1. Business classes are `sealed partial`, laid out as properties, then business rules, then data portal operations. Every operation method carries its attribute.
2. Rules and permissions live only in CSLA business objects. Components read them and never repeat them.
3. Saving returns a new object: always `obj = await obj.SaveAsync()`.
4. No `.resx` files. Interface text is `T("…")` or `[Label("…")]` with a string literal. Error messages are named only by `ErrorMessageType` constants.
5. `Domain` references no EF Core, Npgsql or ASP.NET Core. `Data` never references CSLA. Pages and components never use a `Data` type.
6. Business data is written only through `IUnitOfWork.CommitAsync()`. Only `Platform` code calls `SaveChanges`, `IgnoreQueryFilters` or raw SQL.
7. Each component is `X.razor` (markup only) plus `X.razor.cs` (code). No `@code` or `@functions` blocks, and no inline `style` attribute.
8. Every inline `<script>` carries the CSP nonce; never loosen the CSP.
9. Warnings are errors. Fix the cause; don't suppress the warning.
10. Never commit to `main`. Only the sync workflow writes it. Work on `DEV-<n>-<slug>` branches cut from `claude`.
11. Every commit message starts with the Jira key, for example `DEV-38`.
12. Test first: a failing test before every behaviour.

## Workflow

Branches, pull requests, the `claude` → `main` sync and Jira tracking are in [workflow](docs/claude/workflow.md).

## Guides

- [Getting started](docs/handbook/getting-started.md): prerequisites and commands.
- [Architecture](docs/handbook/architecture.md): the three projects, `Platform` and `Features`, and the boundaries.
- [Workflow](docs/claude/workflow.md): branches, commits, sync to `main`, Claude-only paths, releases.

The design spec, [2026-11-17-csla-starter-rebuild-design.md](docs/superpowers/specs/2026-11-17-csla-starter-rebuild-design.md), is the source of truth until the handbook is complete.

Working on the template itself rather than an app made from it? Read `docs/claude/template-dev.md`.

## Keeping docs current

Any change to a convention, a command or a project updates the matching `CLAUDE.md` and handbook page in the same commit. `CslaStarter.Architecture.Tests` fails when a project folder has no `CLAUDE.md` or a link in a `CLAUDE.md` or `docs/claude/` guide is broken.
