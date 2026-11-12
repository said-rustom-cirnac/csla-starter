# CslaStarter.Data

EF Core 10 with PostgreSQL (Npgsql). It holds:

- `AppDbContext`, which also stores the Data Protection keys;
- entities and configurations;
- migrations;
- the audit interceptor and history reader;
- the security event log;
- and, from the business phase on, the DAL implementations.

**May reference:** Core, Dal, EF Core, Npgsql.

## Rules

- `AddCslaStarterData(connectionString)` registers everything. The host must register an `IDataAccessContext`.
- **Naming:** snake_case. Keys are `Guid.CreateVersion7()`. Entity property names match business property names, so the history view can show friendly names.
- **Concurrency:** roots implement `IAggregateRoot` (`uint Version` = `xmin`), and every aggregate save touches the root. Translate `DbUpdateConcurrencyException` into `Dal.ConcurrencyException`.
- **Auditing:**
  - Annotate entities with `[AuditEntity]`, `[AuditMask]`, `[AuditIgnore]` and `[AuditTypeCode]`, and children with `IHasAuditRoot`.
  - Never write `audit_*` or `security_event` rows yourself.
- **Migrations:** run as `app_owner`. Grants for `app_runtime` are applied by `RuntimeGrants` inside the migrations. Hand-written SQL goes in a class next to the migration.
- **Record-level access** (from the business phase on): `[RecordScoped]` entities get the named query filter `"RecordAccess"`. Only `[BypassRecordAccess]` code may call `IgnoreQueryFilters(["RecordAccess"])`.

Guide: [data access](../../docs/claude/data-access.md).
