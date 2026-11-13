---
name: new-business-object
description: Use when adding a CSLA business object (an editable root with children, a read-only list, or a command) to this app, end to end from the DAL to the tests. Follows docs/claude/recipes/add-a-feature.md.
---

# New business object

Follow [add a feature](../../../docs/claude/recipes/add-a-feature.md) step by step; this is the checklist that keeps nothing out.

1. **Plan the aggregate:** the root, its children, its read-only list row, who may read, create, edit and delete it (by `UserRoleType`), and whether it is record-scoped.
2. **Dal:** DTOs and `I<Name>Dal` in `CslaStarter.Dal/<Feature>/`.
3. **Data:**
   - Entities with `[AuditEntity]` (children `IHasAuditRoot`, roots `IAggregateRoot`), `[RecordScoped]` if needed, and `[AccessGrant]` on rows that grant access.
   - A configuration, a migration (`dotnet ef migrations add <Name> -p src/CslaStarter.Data -s src/CslaStarter.Data`), and the DAL.
   - The DAL uses `ExpectVersion`, `TouchRoot` and `SaveAggregateAsync`, with your unique indexes.
   - Register it in `AddCslaStarterData`.
4. **Business:** classes laid out as registrations, properties, rules, object authorization, data portal operations.
   - Every property has `[LocalizedDisplayName]`.
   - Rules use `Func<string>` messages.
   - Each (property, action) has exactly one `HasRole`.
   - Async rules that read data use `RunMode = DenyOnServerSidePortal`.
   - Invariants are checked on save as well.
5. **Resources:** add `Labels` keys (`{EntityType}_{Property}`, so the history shows them) and `Messages` keys, in English and French.
6. **Tests:**
   - Business.Tests: every rule; role × property × action; the save round trip.
   - Integration.Tests: the DAL against PostgreSQL, record access per role, concurrency.
7. **Pages:** follow the `new-component` skill or [add a page](../../../docs/claude/recipes/add-a-page.md).
8. **Run** `dotnet test` and `dotnet format --verify-no-changes`, and update the guides your change touches.
