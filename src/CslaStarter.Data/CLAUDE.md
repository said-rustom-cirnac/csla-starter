# CslaStarter.Data

EF Core 10 with PostgreSQL (Npgsql): `AppDbContext`, entities, migrations, the DAL implementations, the audit interceptor, the draft store, the security event log and the Data Protection key store.

**May reference:** Core, Dal, EF Core, Npgsql.

## Rules

- **Naming:** snake_case table and column names (`UseSnakeCaseNamingConvention`). Entity property names match business property names, so the history view can show friendly names.
- **Concurrency:** per aggregate. Every save updates the root row, and the root's `xmin` (`[Timestamp] uint Version`) is checked. Translate `DbUpdateConcurrencyException` into `Dal.ConcurrencyException`.
- **Record-level access:** `[RecordScoped]` entities get the named query filter `"RecordAccess"`. Only `[BypassRecordAccess]` code may call `IgnoreQueryFilters(["RecordAccess"])`.
- **Audit:** captured automatically in the same transaction. Never write audit rows by hand.
- **Migrations:** run by the deployment pipeline as `app_owner`. The app connects as `app_runtime` and never migrates outside Development.

Guides: [architecture](../../docs/claude/architecture.md).
