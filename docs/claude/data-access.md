# Data access

## The database

- PostgreSQL **15 or later** (tests use 18) through EF Core 10 + Npgsql. From version 15, PostgreSQL no longer lets every role create objects in `public`, which the least-privilege model relies on. Table and column names are snake_case (`UseSnakeCaseNamingConvention`), and every configuration lives in `src/CslaStarter.Data/Configurations`.
- Primary keys are `Guid`s generated in the app with `Guid.CreateVersion7()`. Audit and security tables use `bigint` identity columns.
- **Aggregate roots** implement `IAggregateRoot` with `[Timestamp] uint Version`, which maps to PostgreSQL `xmin`.
  - Concurrency is checked **per aggregate**: every save of an aggregate updates its root row, so any change anywhere in it bumps the root's version.
  - DALs turn `DbUpdateConcurrencyException` into `Dal.ConcurrencyException`.
- `AppDbContextOptions.UseAppDatabase(connectionString)` is the single place provider settings live. The app, tests and `dotnet ef` all use it.

## Two roles

| Role | Used by | Can |
|---|---|---|
| `app_owner` | Migrations (deployment pipeline), `bootstrap-admin` | Owns the database and every table |
| `app_runtime` | The running app (`ConnectionStrings:App`) | SELECT/INSERT/UPDATE/DELETE on app tables. On `audit_event`, `audit_change` and `security_event`: SELECT and INSERT only. On the migration history: SELECT only. No DDL. |

- `deploy/sql/create-roles.sql` creates both roles once per server: `psql -v owner_password=... -v runtime_password=... -f deploy/sql/create-roles.sql`.
- Grants are applied by the migrations themselves (`Migrations/RuntimeGrants.cs`), so every new table is covered automatically.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/CslaStarter.Data -s src/CslaStarter.Data
CSLASTARTER_DESIGN_CONNECTION="<owner connection string>" dotnet ef database update -p src/CslaStarter.Data -s src/CslaStarter.Data
```

- In production, migrations run from the deployment pipeline as `app_owner`. The app never migrates itself.
- `/health/ready` reports Unhealthy while migrations are pending.
- Generated migration files are excluded from analyzers in `.editorconfig`. Hand-written SQL goes in a separate class beside the migration, such as `RuntimeGrants` and `AppendOnlySql`.

## Audit capture

`AuditSaveChangesInterceptor` runs in every `SaveChanges` call:

- **Stamping:** it fills `CreatedOn/By` and `ModifiedOn/By` on `IAuditedEntity`, using the injected `IDataAccessContext` and `TimeProvider`.
- **Change rows:** it writes one `audit_event` per inserted, updated or deleted row, plus one `audit_change` per changed property with old and new values as invariant-culture strings. The rows go in **the same transaction** as the change.
- **No-op updates:** a row marked Modified with no audited change (a root "touched" for its aggregate's version check, or an `[AuditIgnore]`-only change) is still saved and stamped, which bumps its `xmin`, but it adds nothing to the history.
- **Failed saves:** if a save fails (concurrency, constraint), the next save on that context discards the audit rows the failed one added, so the history never records a change that didn't happen.
- **Never audited:** `Version`, `CreatedOn/By`, `ModifiedOn/By`, and the single-column surrogate key.

Attributes on entities (`CslaStarter.Data.Auditing`):

| Attribute | Effect |
|---|---|
| `[AuditEntity("User")]` | The entity type name the history is recorded under. It matches the business object. |
| `[AuditMask]` | Records "changed", never the values |
| `[AuditIgnore]` | Skips a property, or a whole entity class |
| `[AuditTypeCode("UserRoleType")]` | The values are codes of that lookup, so the history shows descriptions |
| `IHasAuditRoot` | A child row whose history appears under its aggregate root |

The audit tables are append-only. A trigger rejects UPDATE, DELETE and TRUNCATE for every role, including the owner.

`IAuditHistoryReader` (`AuditHistoryReader`) reads a record's history, its children included, newest first. It can filter to one property.

## Who is making the change

The data layer never asks for the user directly. It reads `IDataAccessContext` (`UserId`, `UserName`, `CorrelationId`, `OriginId`). Each place that writes data registers the right one:

| Code path | Context |
|---|---|
| HTTP requests | `HttpContextDataAccessContext` |
| CSLA data portal | The CSLA context (added by the business layer) |
| Platform operations (first sign-in, `bootstrap-admin`) | `SystemDataAccessContext("sign-in")` / `("bootstrap-admin")` with their own short-lived contexts |

## Keys

App-generated `Guid` keys are configured with `ValueGeneratedNever()`. Otherwise EF Core treats a new child that already has its id (found through a navigation) as an existing row, issues an UPDATE, and fails with a concurrency error. Every new entity with a `Guid` key needs this in its configuration.
