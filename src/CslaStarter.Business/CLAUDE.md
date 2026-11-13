# CslaStarter.Business

CSLA 10.2 business objects: validation, permissions and data portal operations. This is the single source of truth for business rules.

**May reference:** Core, Dal, and the `Csla` package. Never EF Core or Npgsql.

## Layout

| Folder | Holds |
|---|---|
| `Base/` | `AppBusinessBase<T>`, `AppRootBase<T>`, `RuleInspector` |
| `Types/` | `CslaType<T>`, `CslaTypeList`, `UserRoleType`, `ProjectStatusType` |
| `Rules/` | `HasRole`, `IsSignedIn`, `SupportedLanguage` |
| `Security/` | `CslaCurrentUser`, `CslaDataAccessContext` |
| `Users/` | `UserEdit`, `UserProfile`, `UserList` |
| `Projects/` | the sample feature |
| `Drafts/` | `DraftCodec`: a business object graph to draft bytes and back |

## Rules

- **Class layout:** registrations, properties, rules, object authorization, data portal operations, enforced by tests. See [business objects](../../docs/claude/business-objects.md).
- **Labels:** every registered property has `[LocalizedDisplayName(nameof(Labels.Key))]` in both languages, enforced by tests.
- **Rule messages** use `Func<string>` overloads that read `Messages`.
- **Role checks:** `HasRole(action, [property], params int[] UserRoleType codes)`, one rule per (property, action).
- **Async rules** that need data run a command through the data portal (they never resolve a DAL directly), set `RunMode = RunModes.DenyOnServerSidePortal`, and leave the save to a database constraint. Register `UseCslaStarterRules()` in `AddCsla` so a failing rule breaks instead of ending the process.
- **Aggregate invariants** are checked on save too (member changes, closing), not only in helper methods.
- **Aggregates save as one DTO round trip** and reload from the result. Children never call the DAL.
- **Criteria are primitives**, not records or `CriteriaBase`.

## CSLA gotchas

- Saving returns a new instance: always `obj = await obj.SaveAsync()`.
- A CSLA object keeps its scope's `ApplicationContext`, so never cache or share one across users. (`TypeDal` caches lookup rows instead.)
- A data portal call from a circuit runs in a new DI scope, but **calls made inside a data portal operation share its scope** (and its `DbContext`). The user, culture and origin come through `ApplicationContext`.
- `SetProperty` throws without write permission. `GetProperty` returns the default (`""` or `null`) without read permission.
- Field types must be types MobileFormatter supports: primitives, string, decimal, DateTime/DateTimeOffset, DateOnly/TimeOnly, Guid, byte[], enums, nullables and `List<int>`. No dictionaries or POCOs.
- With nullable reference types on, don't set properties in constructors. Use `[Create]`.
- Data portal operations are instance methods (CSLA0015). `UseLegacyOperationMethods` is off.

Guides: [business objects](../../docs/claude/business-objects.md), [conventions](../../docs/claude/conventions.md); recipes in [docs/claude/recipes](../../docs/claude/recipes/add-a-feature.md).
