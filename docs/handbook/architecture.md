# Architecture

## Three projects

The solution has three projects under `src/`.

| Project | What it holds | What it references |
|---|---|---|
| `CslaStarter.Domain` | The CSLA business objects with their rules and permissions, the DAL contracts and DTOs they call, and the translator. | No other project |
| `CslaStarter.Data` | EF Core and PostgreSQL: entities, the unit of work, audit, and the DAL implementations. | `Domain` |
| `CslaStarter.Web` | The host, the `Csla*` component kit, the pages, and the vendored GC Design System in `wwwroot/gcds`. | `Domain` and `Data` |

The code arrives slice by slice. Today `Domain` holds the text entry point, the rule attributes, the base classes and the Users business objects, tested against fake DALs. `Data` is still empty, and `Web` answers only a liveness check at `/health/live`. The design system files are in place; no page uses them yet.

## Platform and Features

Each project has two top-level folders:

- `Platform/` belongs to the starter and is the same in every app: the framework code, the component kit, and the features every app needs, such as users and roles, translations, error messages, audit and history.
- `Features/` is the app's own code, with one folder per feature.

## Boundaries

`tests/CslaStarter.Architecture.Tests` fails if any of these is broken:

- Each project references only the projects in the table above.
- `Domain` references no EF Core, Npgsql or ASP.NET Core assembly.
- The compiled `Data` assembly does not reference CSLA. It sees only the DAL contracts and DTOs.
- Pages and components never use a `Data` type. They reach data only through the CSLA data portal.
- `Platform` never references `Features`.
- A feature may use anything in `Platform`, but never another feature's entities or DAL. Features share through business objects.
- Only `Platform` code in `Data` calls `SaveChanges`, `IgnoreQueryFilters` or raw SQL.

## Tests

Tests live under `tests/`. Today there are two test projects: `CslaStarter.Architecture.Tests` (boundaries, conventions, documentation) and `CslaStarter.Domain.Tests` (business objects and rules, with fake DALs). Each other test project is added with the first code it tests.

## Build settings

Nullable reference types are on, and warnings are errors, the analyzers' included. NuGet audit checks every package. Package versions are set in one place, `Directory.Packages.props`, and each project commits a `packages.lock.json`. Builds are deterministic.
