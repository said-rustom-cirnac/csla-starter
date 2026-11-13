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
  - Every `[AuditEntity]` name needs a business type marked `[HistoryOf]` with the same name, or its history can't be read (enforced by tests).
- **Migrations:** run as `app_owner`. Grants for `app_runtime` are applied by `RuntimeGrants` inside the migrations. Hand-written SQL goes in a class next to the migration.
- **Record-level access** (from the business phase on): `[RecordScoped]` entities get the named query filter `"RecordAccess"`. Only `[BypassRecordAccess]` code may call `IgnoreQueryFilters(["RecordAccess"])`.
- **Change notices** are published by `ChangePublishingInterceptor` after commit, one per aggregate root. Mark rows that grant access with `[AccessGrant(nameof(UserId), …)]` so the user gets an access-changed notice. Never publish by hand.
- **Drafts:** `DraftStore` (encrypted, per user, 8 hours) uses its own short-lived contexts.

Guides: [data access](../../docs/claude/data-access.md), [realtime](../../docs/claude/realtime.md).
