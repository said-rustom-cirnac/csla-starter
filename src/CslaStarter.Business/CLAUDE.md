# CslaStarter.Business

CSLA 10.2 business objects: validation, permissions and data portal operations. This is the single source of truth for business rules.

**May reference:** Core, Dal, and the `Csla` package. Never EF Core or Npgsql.

## Rules

- Follow the class layout in [conventions](../../docs/claude/conventions.md): registrations, properties, rules, data portal operations.
- Every property has `[LocalizedDisplayName(nameof(Labels.Key))]`.
- Rule messages use the `Func<string>` overloads. Rules are registered once per type per process, so a string captured at registration time would be stuck in one language.
- **Role checks:** `HasRole(action, [property], params int[] roleCodes)`. CSLA evaluates only the **first** rule per (property, action), so put every allowed role in one rule.
- **Lookups:** `*Type` classes derive from `CslaType<T>` and expose `public const int` codes.

## CSLA gotchas

- Saving returns a new instance (`AutoCloneOnUpdate`): always `obj = await obj.SaveAsync()`.
- Each data portal call runs in a new DI scope, off the UI thread. The user, culture and origin reach the data layer through `ApplicationContext`, not through scoped services.
- `SetProperty` throws without write permission. `GetProperty` returns the default without read permission.
- Field types must be types MobileFormatter supports: primitives, string, decimal, DateTime/DateTimeOffset, DateOnly/TimeOnly, Guid, byte[], enums, and nullable versions. No `List<string>`, dictionaries or POCOs; use child objects instead.
- With nullable reference types on, don't set properties in constructors. Use `[Create]`.
- `UseLegacyOperationMethods` is off, so use attributed operations (`[Fetch]` and so on), never `DataPortal_XYZ`.

Guides: [architecture](../../docs/claude/architecture.md), [conventions](../../docs/claude/conventions.md).
