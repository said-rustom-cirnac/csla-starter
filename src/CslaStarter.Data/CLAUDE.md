# CslaStarter.Data

EF Core 10 on PostgreSQL 18 (Npgsql): the database model, its migration, and the implementations of the DAL and store contracts in `Domain`. The unit of work and audit capture are not built yet.

## What it holds today

- `Platform/AppDbContext.cs`: the one context, with `Users`, `Translations` and `ErrorMessages`. It applies every `IEntityTypeConfiguration` in this assembly.
- `Platform/DataServices.cs`: `AddAppData(connectionString)` registers the `IDbContextFactory<AppDbContext>` (Npgsql, snake_case names), every class here that implements an `I…Dal` interface (as that interface, scoped, found by a scan, so a new DAL needs no registration line), `IUserDirectory` and `TextSeeder` (both singletons).
- `Platform/AppDbContextFactory.cs`: the design-time factory for `dotnet ef`. It reads the connection string from `CSLASTARTER_DESIGN_CONNECTION` and throws if it is not set.
- `Platform/Users/`: the `app_user` entity and configuration; `UserDal` (`IUserDal`); `UserDirectory` (`IUserDirectory`), which creates a person's account at first sign-in.
- `Platform/Texts/`: the `translation_type` and `error_message_type` entities and their configuration; `TextDal` (`ITextDal`); `TextSeeder`, which inserts the shipped rows from `Seed/translations.json` and `Seed/error-messages.json` that the database lacks and never changes a row that exists.
- `Migrations/`: one migration, `Initial` (see below).
- `Seed/`: the shipped text. Rows are `{ "source", "en", "fr" }` and `{ "code", "en", "fr" }`. Both files are embedded in this assembly under the fixed names `translations.json` and `error-messages.json`; `Domain` embeds `error-messages.json` too, as its fallback.
- `Features/` is still empty.

## Patterns to copy

- **An entity and its configuration:** `Platform/Users/AppUser.cs` and `Platform/Users/AppUserConfiguration.cs`. The entity is a plain `sealed` class with a `Guid.CreateVersion7()` id set in code. The configuration is `internal sealed`, names the table (`ToTable("app_user")`), sets `ValueGeneratedNever()` on the id, gives every string a maximum length, and maps a `uint Version` with `IsRowVersion()`, which Npgsql maps to PostgreSQL's `xmin` (no column is created). Add a `DbSet` to `AppDbContext`, then regenerate the migration.
- **A DAL:** `Platform/Users/UserDal.cs`. It implements the `I…Dal` contract from `Domain`, reads with `AsNoTracking()`, projects straight into the DTO, carries `xmin` as the DTO's `Version`, and throws `NotFoundException("User", id)` for a missing row. For now its reads use their own context from the factory, and `StageUpdate` throws `NotImplementedException`: writes wait for the unit of work.
- **A platform store** (writes on its own, outside any business transaction): `Platform/Users/UserDirectory.cs` and `Platform/Texts/TextDal.cs`. Each call creates its own short-lived context from `IDbContextFactory<AppDbContext>` and depends on nothing else, so a singleton can hold it. An insert that may race uses raw SQL `insert … on conflict (…) do nothing`, then reads the row, so the caller never sees a unique violation.

## The migration

Until the rebuild merges there is one migration, `Initial`. After a model change, delete it and add it again (adding a migration never connects to the database, so any valid connection string will do):

```bash
dotnet tool restore
rm -r src/CslaStarter.Data/Migrations
CSLASTARTER_DESIGN_CONNECTION="Host=localhost;Database=cslastarter" dotnet ef migrations add Initial -p src/CslaStarter.Data -s src/CslaStarter.Data
```

The integration tests apply it to a fresh database on every run, so a migration that is out of step with the model fails them.

## Rules

- References `CslaStarter.Domain` only, and uses only its DAL contracts, DTOs and exceptions. The compiled assembly never references `Csla`.
- Only `Platform/` code calls `SaveChanges`, `IgnoreQueryFilters` or raw SQL. A feature never uses another feature's entities or DAL.
- Tests: `tests/CslaStarter.Integration.Tests` runs the DALs and stores against a real PostgreSQL (see its `CLAUDE.md`). `tests/CslaStarter.Architecture.Tests` checks the references and boundaries.

## Pitfalls

- **CSLA is visible here.** `Csla` reaches this project through the `Domain` reference, so `using Csla;` compiles. Do not use it: `BoundaryTests.Data_DoesNotReferenceCsla` fails on any CSLA type in the compiled assembly.
- **Raw SQL names the tables and columns** in snake_case (`app_user`, `tenant_id`). Renaming a property or a table means changing that SQL too; the integration tests catch a mismatch.
- **Database defaults:** `added_date` is set by the database (`now()`), so EF leaves it out of inserts. `is_active` has no database default: EF would skip a `false` value on a `bool` with a default, so it is always set in code or in the insert.
- **`dotnet ef` needs `CSLASTARTER_DESIGN_CONNECTION`**, even to add a migration. Without it the design-time factory stops with a message naming the variable.

Structure and boundaries: [architecture](../../docs/handbook/architecture.md).
