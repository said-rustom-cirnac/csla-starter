# CslaStarter.Dal

The data access contract: one DAL interface per aggregate, the DTOs it exchanges, and the DAL exceptions. Business calls it; Data implements it.

**May reference:** Core.

## Rules

- **Interfaces:** `I<Thing>Dal`, all async, each method taking a `CancellationToken`.
  - `NotFoundException` means "doesn't exist, or the user may not see it"; the two are never told apart.
  - `ConcurrencyException` means the aggregate changed since its version was loaded.
- **DTOs:** records.
  - `XDto` (read) carries `Version` and the audit stamps.
  - `XSaveDto` is the whole aggregate. Children are matched by key, and a child missing from the DTO is removed.
  - `XInfoDto` is a list row, and `PagedResult<T>` is one page plus the total.
- **Shared access facts** both layers need live here, for example `ProjectAccess.SeesAllRoles`. A test keeps them in step with the business constants.
- **No EF types**, ever.

Guides: [data access](../../docs/claude/data-access.md), [add a feature](../../docs/claude/recipes/add-a-feature.md).
