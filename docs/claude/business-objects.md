# Business objects (CSLA 10.2)

## Base classes (`src/CslaStarter.Business/Base`)

| Use | For |
|---|---|
| `AppRootBase<T>` | An aggregate root. Adds `Version` (the root row's `xmin`) plus everything below. |
| `AppBusinessBase<T>` | Editable objects, including children. Provides the audit stamps (`CreatedOn/By`, `ModifiedOn/By`), `EntityType`/`EntityKey` (`IEntityIdentity`), `PropertyHasRule(name, "Required")` and `MaxLengthOf(name)` for components. |
| `ReadOnlyBase<T>` / `ReadOnlyListBase<TList,TItem>` | List rows and lists. Lists keep a `TotalCount` through `OnGetState`/`OnSetState`. |
| `CslaType<T>` / `CslaTypeList<TList,T>` | `*Type` lookups (see [add a type](recipes/add-a-type.md)). |
| `CommandBase<T>` | Actions such as `CloseProjectCommand` and `ProjectCodeExistsCommand`. |

Classes are `[Serializable]` and `sealed` unless they are bases.

## Layout

The layout is enforced by `BusinessLayoutTests`:

1. `public const int` codes (types only)
2. Property registrations: `public static readonly PropertyInfo<T> XProperty = RegisterProperty<T>(nameof(X));`
3. Properties: `[LocalizedDisplayName(nameof(Labels.Area_X))]`, with a body that only calls `GetProperty`/`SetProperty`/`LoadProperty`/`ReadProperty` (`!` allowed for non-nullable strings)
4. `AddBusinessRules()`
5. `[ObjectAuthorizationRules] public static void AddObjectAuthorizationRules()`, using static `BusinessRules.AddRule(typeof(X), …)`
6. Data portal operations: `[Create]`, `[Fetch]`, `[Insert]`, `[Update]`, `[DeleteSelf]`, `[Delete]`, `[Execute]`, `[CreateChild]`, `[FetchChild]`. They are instance methods, never static.

Helpers such as `AddTask`, `AssignRole`, `CanEdit` and `Load` may sit anywhere.

CSLA's own analyzer CSLA0007 is turned off for Business in `.editorconfig`, because it flags the `GetProperty(...)!` that non-nullable strings need. `RegisteredProperties_OnlyUsePropertyMethods` enforces the same rule properly.

## Rules

- **Messages:** always use the `Func<string>` overloads with `Messages.*`, for example `new Required(NameProperty, () => Messages.Common_Required)`. CSLA formats `{0}` with the property's friendly name in the current language.
- **Custom rules** live in `Business/Rules` (shared) or `<Feature>/Rules`, and format their message through the friendly name too (see `EndAfterStart`, `NotNegative`, `SupportedLanguage`).
- **Async rules** (`BusinessRuleAsync`) mark the property busy, which components show as "Checking…". If a rule needs data, it runs a **command through the data portal** (see `UniqueProjectCode` → `ProjectCodeExistsCommand`), so the check gets its own scope. Never resolve a DAL inside a rule.
- **Several properties:** use `Dependency(StartDateProperty, EndDateProperty)` when a change to one must re-run the other's rules.

## Permissions

| Rule | Use for |
|---|---|
| `HasRole(action, [property], params int[] codes)` | Role checks with `UserRoleType` constants |
| `IsSignedIn(action)` | Any signed-in user |
| `HasRole(action)` (no roles) | Nobody, for example "users are never created through this object" |

- CSLA evaluates only the **first** rule per (property, action), so list every allowed role in one `HasRole`.
- A property without read permission returns its default (`""` or `null`), and a setter without write permission throws `Csla.Security.SecurityException`.
- For child collections, guard the root's add and remove methods with `CanWriteProperty(XProperty)`, as `AssignRole` and `AddMember` do.
- **Per-record decisions** happen in the operation: `CloseProjectCommand` checks "Admin, or a Technical member of this project".
- **Who sees which records** is the data layer's job, through record-level access. A hidden record simply isn't found.
- **Static helpers** for the UI: `X.CanEdit(applicationContext)`, `X.CanCreate(applicationContext)`.

## The current user

- `ICurrentUser` (`CslaCurrentUser`) and `IDataAccessContext` (`CslaDataAccessContext`) read CSLA's `ApplicationContext`: the circuit's user, or the caller inside the data portal.
- Inject them into operations with `[Inject] ICurrentUser user`, and check `user.HasRole(UserRoleType.Admin)`.

## Data portal patterns

- **Aggregates save as one DTO round trip:**
  1. `Insert` or `Update` builds a `*SaveDto` with the root's fields and every child.
  2. The DAL reconciles the children by key in one `SaveChanges`, checking the root `Version`.
  3. The object reloads from the returned DTO (`Load(...)`), so the children come back clean and `Version` is current.
  
  Children never call the DAL themselves.
- **Create a child** through `IChildDataPortal<TChild>.CreateChild(...)`, from the root's add methods.
- **Criteria are primitives.** CSLA 10 obsoletes `CriteriaBase` and rejects records. Lists take `(string search, int code, …)`, where `""` and `0` mean no filter.
- **Saving returns a new object:** `project = await project.SaveAsync();`
- **Never share a CSLA object across users or scopes.** It keeps its scope's `ApplicationContext`. Fetch type lists where they're used; `TypeDal` caches the rows.

## Tests

See [testing](testing.md). Rules and permissions are tested with `CslaTestHost` and fake DALs in `CslaStarter.Business.Tests`. DALs are tested against PostgreSQL in `CslaStarter.Integration.Tests`.
