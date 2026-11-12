# CSLA Starter

Jira project epic: DEV-37 (CSLA-starter)

## What this is

A starter for Government of Canada line-of-business web apps: .NET 10 Blazor Server, CSLA 10.2 business objects, EF Core with PostgreSQL, Entra ID sign-in, English and French, and a Protected B security baseline. Business rules and permissions live in CSLA objects; reusable `Csla*` components display them without repeating them.

## Commands

| Task | Command |
|---|---|
| Build (warnings are errors) | `dotnet build -c Release` |
| Test (xUnit v3 on Microsoft Testing Platform) | `dotnet test` |
| Script tests (sync and guard) | `bash scripts/tests/run.sh` |
| Format check, as CI runs it | `dotnet format --verify-no-changes` |
| Run the app | `dotnet run --project src/CslaStarter.Web` |
| After adding or changing a package | `dotnet restore --force-evaluate`, then commit the changed `packages.lock.json` files |

CI restores in locked mode, so a package change without updated lock files fails the build.

## Architecture map

```
Core           → (none)
Dal            → Core
Business       → Core, Dal
Data           → Core, Dal
Infrastructure → Core, Data
Components     → Core, Business
Web            → all (composition root only)
```

`CslaStarter.Architecture.Tests` fails if a reference breaks this graph.

- [CslaStarter.Core](src/CslaStarter.Core/CLAUDE.md): contracts and localization resources shared by every layer.
- [CslaStarter.Dal](src/CslaStarter.Dal/CLAUDE.md): DAL interfaces, DTOs and DAL exceptions.
- [CslaStarter.Business](src/CslaStarter.Business/CLAUDE.md): CSLA business objects, rules and permissions.
- [CslaStarter.Data](src/CslaStarter.Data/CLAUDE.md): EF Core + PostgreSQL, DAL implementations, audit capture.
- [CslaStarter.Infrastructure](src/CslaStarter.Infrastructure/CLAUDE.md): live updates, presence, sign-in plumbing, telemetry.
- [CslaStarter.Components](src/CslaStarter.Components/CLAUDE.md): the `Csla*` component family.
- [CslaStarter.Web](src/CslaStarter.Web/CLAUDE.md): the host and composition root.
- [CslaStarter.Architecture.Tests](tests/CslaStarter.Architecture.Tests/CLAUDE.md): layer, convention and documentation checks.

## Golden rules

1. CSLA classes are laid out as property registrations, then properties, then business rules, then data portal operations ([conventions](docs/claude/conventions.md)).
2. Validation and permissions live only in CSLA business objects. Components read them and never repeat them.
3. Saving returns a new object: always `obj = await obj.SaveAsync()`.
4. Rule messages use the `Func<string>` overloads, so they follow the user's language.
5. Every business property has `[LocalizedDisplayName(...)]`, and every label exists in English and French.
6. Each component is `X.razor` (markup only) plus `X.razor.cs` (code). No `@code` or `@functions` blocks anywhere.
7. Never commit to `main`. Only the sync workflow writes it. Work on `DEV-<n>-<slug>` branches cut from `claude`.
8. Every commit message starts with the Jira key, for example `DEV-38`.
9. Warnings are errors. Fix the cause; don't suppress the warning.

## Workflow

Branches, pull requests, the `claude` → `main` sync and Jira tracking are in [workflow](docs/claude/workflow.md).

## Guides

- [Architecture](docs/claude/architecture.md): projects, layers and the key design decisions.
- [Conventions](docs/claude/conventions.md): naming, class layout, razor files, nullable rules.
- [Workflow](docs/claude/workflow.md): branches, commits, sync to `main`, Claude-only paths.
- [Testing](docs/claude/testing.md): running tests, filters, what each test project covers.

Working on the template itself rather than an app made from it? Read `docs/claude/template-dev.md`.

## Keeping docs current

Any change to a convention, a command or a project updates the matching `CLAUDE.md` and guide in the same commit. `CslaStarter.Architecture.Tests` fails when a project folder has no `CLAUDE.md` or a guide link is broken.
