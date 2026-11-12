# Plan 3: Business Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The CSLA 10.2 business layer, with:

- CSLA wired into the host;
- base classes, `CslaType<T>` lookups and the `HasRole` rule;
- users and roles;
- the Projects/Tasks/Members sample, with record-level access;
- DALs on PostgreSQL;
- convention tests that keep every business class in the agreed shape.

**Architecture:**

- **Business** holds CSLA objects only. It reads the user from CSLA's `ApplicationContext` (`CslaCurrentUser`, `CslaDataAccessContext`), and data comes through DAL interfaces injected into data portal methods.
- **Dal** holds the contracts plus the one shared fact that both layers need for record-level access: which roles see every project.
- **Data** implements the DALs with EF Core. It adds the `"RecordAccess"` named query filter and per-aggregate concurrency: the audit interceptor keeps a root Modified when its children change.

**Tech Stack:** Csla, Csla.AspNetCore and Csla.Blazor 10.2.0, Csla.Testing 10.2.0, EF Core 10 (named query filters), Microsoft.CodeAnalysis.CSharp 5.9.0 (convention test).

**Spec:** `docs/superpowers/specs/2026-11-12-csla-starter-design.md`, sections 6, 6.5, 7, 9.1, 13 and 16.

## Global Constraints

- **CSLA configuration:**

  ```csharp
  AddCsla(o => o.AddAspNetCore().AddServerSideBlazor(b => b.UseCslaPermissionsPolicy = true).DataPortal(dp => { dp.UseLegacyOperationMethods = false; dp.AddServerSideDataPortal(); }))
  ```

  - Never inject `StateManager`.
  - Never use the AutoImplementProperties generator.
- **Class layout:** `public const int` codes (types only), then property registrations, then properties, then `AddBusinessRules`, then `[ObjectAuthorizationRules]`, then data portal operations.
- **Properties and rules:**
  - Every registered property has `[LocalizedDisplayName(nameof(Labels.X))]`, with both languages in `Labels`.
  - Rule messages use `Func<string>` overloads that read `Messages`.
- **Role checks:** `HasRole(action, [property], params int[] roleCodes)`, one rule per (property, action).
  - Role codes: `UserRoleType.Admin = SystemRoles.Admin = 1`, `Client = 2`, `Technical = 3`.
  - Project status codes: `Planned = 1`, `Active = 2`, `OnHold = 3`, `Closed = 4`.
- **Saving:** `obj = await obj.SaveAsync()`. Never set properties in constructors (use `[Create]`).
- **Field types:** only MobileFormatter-supported types. Use `DateOnly` for dates and `decimal` for money.
- **Record-level access:** roles `Admin` and `Technical` see every project. A `Client` sees only projects where they are a member.
  - A hidden record reads as **not found**.
  - Only `[BypassRecordAccess]` code may call `IgnoreQueryFilters(["RecordAccess"])`.
- **Concurrency:** per aggregate. Any child change updates the root row, which bumps `xmin`, and the DAL checks the version the user loaded.
- **Property permissions (sample):**
  - `ProjectEdit.Budget`: read Admin and Technical, write Admin.
  - `UserEdit.EmployeeNumber`: read and write Admin.
  - `UserEdit.Roles` and `UserEdit.IsActive`: write Admin.

## Review Focus

1. **A Client fetching a project by id that they aren't a member of must get not found,** not the record and not "access denied". *Pinned by* `ProjectDal_Fetch_HiddenProject_ThrowsNotFound` (Task 4).
2. **A Client's project list must have totals and pages that count only their projects.** *Pinned by* `ProjectDal_List_CountsOnlyVisibleProjects` (Task 4).
3. **Another user adding a task to a project I have open must make my save fail,** even though my root fields didn't change. *Pinned by* `ProjectDal_ChildChangedByOtherUser_RootSaveFails` (Task 4).
4. **Saving a project with only a task change must still record an Update under the project's history,** and must not reset the root to Unchanged. *Pinned by* `Audit_ChildOnlyChange_TouchesRootVersion` (Task 4).
5. **Closing a project with open tasks must fail with a clear error.** *Pinned by* `CloseProject_WithOpenTasks_Fails` (Task 5).

---

### Task 1: CSLA wiring, current user and data access context

**Files:**
- Create in `src/CslaStarter.Core/`: `Security/AppClaimTypes.cs` (moved from `EntraClaims` constants), `Security/ICurrentUser.cs`, `IEntityIdentity.cs`
- Modify: `src/CslaStarter.Core/Security/IDataAccessContext.cs` (add `IReadOnlySet<int> RoleCodes`), all its implementations, `src/CslaStarter.Infrastructure/Identity/EntraClaims.cs` (use `AppClaimTypes`)
- Create in `src/CslaStarter.Business/`: `Security/CslaCurrentUser.cs`, `Security/CslaDataAccessContext.cs`, `BusinessServiceCollectionExtensions.cs`
- Modify: `src/CslaStarter.Web/Program.cs` (`AddCsla`, `AddCslaStarterBusiness`; remove `HttpContextDataAccessContext` registration and delete the class)
- Test: `tests/CslaStarter.Business.Tests/CurrentUserTests.cs`

**Interfaces (Produces):**

```csharp
public static class AppClaimTypes { const string UserId = "csla_starter:user_id"; const string Language = "csla_starter:lang"; const string Inactive = "csla_starter:inactive"; }
public interface IEntityIdentity { string EntityType { get; } string EntityKey { get; } }
public interface ICurrentUser { bool IsAuthenticated { get; } Guid? UserId { get; } string DisplayName { get; } IReadOnlySet<int> RoleCodes { get; } string PreferredLanguage { get; } bool HasRole(int roleCode); bool HasAnyRole(params int[] roleCodes); }
public sealed class CslaCurrentUser(ApplicationContext applicationContext) : ICurrentUser;          // reads ApplicationContext.User
public sealed class CslaDataAccessContext(ApplicationContext applicationContext) : IDataAccessContext; // same principal; OriginId from ClientContext["origin"]
public static IServiceCollection AddCslaStarterBusiness(this IServiceCollection services); // ICurrentUser, IDataAccessContext (scoped)
```

- [ ] **Step 1: Write the failing tests** (they use `CslaTestHost`):
  - `CurrentUser_ReadsRolesAndIdFromPrincipal`: a principal with `AppClaimTypes.UserId` and role claims "1" and "3" gives `HasRole(1)`, `HasAnyRole(2, 3)` and `!HasRole(2)`.
  - `CurrentUser_Anonymous_HasNoRoles`
  - `DataAccessContext_UsesPrincipalNameAndId`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - Add `Csla` to Business, `Csla.AspNetCore` and `Csla.Blazor` to Web, and `Csla.Testing` to Business.Tests.
  - Register CSLA in `Program.cs` exactly as in Global Constraints.
- [ ] **Step 4: Run the tests.** Run `dotnet test`. Expected: all green, and integration tests still pass with the CSLA context.
- [ ] **Step 5: Commit** with the message `DEV-38 Wire CSLA and read the current user from ApplicationContext`.

### Task 2: Base classes, CslaType lookups and the HasRole rule

**Files:**
- Create in `src/CslaStarter.Business/`:
  - `Base/AppBusinessBase.cs`, `Base/AppRootBase.cs`, `Base/AppReadOnlyListBase.cs`, `Base/RuleInspector.cs`
  - `Types/CslaType.cs`, `Types/CslaTypeList.cs`, `Types/TypeListCache.cs`, `Types/UserRoleType.cs`, `Types/ProjectStatusType.cs`
  - `Rules/HasRole.cs`
- Create in Dal: `Dal/ITypeDal.cs` + `TypeDto`. In Data: `Data/Dal/TypeDal.cs`, and `Data/Entities/TypeRow.cs` (`UserRoleTypeRow` derives from it)
- Test: `tests/CslaStarter.Business.Tests/TypeTests.cs`, `HasRoleTests.cs`

**Interfaces (Produces):**

```csharp
public abstract class AppBusinessBase<T> : BusinessBase<T>, IEntityIdentity where T : AppBusinessBase<T>
{ CreatedOn, CreatedBy, ModifiedOn, ModifiedBy (read-only, LoadProperty); abstract string EntityType; abstract string EntityKey; public bool PropertyHasRule(string propertyName, string ruleTypeName); public int? MaxLengthOf(string propertyName); }
public abstract class AppRootBase<T> : AppBusinessBase<T> { public uint Version { get; } } // root only
public abstract class CslaType<T> : ReadOnlyBase<T> { int Code; string DescriptionEn; string DescriptionFr; bool IsActive; int SortOrder; string Description /* current UI culture */ }
public abstract class CslaTypeList<TList, TItem> : ReadOnlyListBase<TList, TItem> { TItem? Find(int code); IEnumerable<TItem> ActiveOrCurrent(int? currentCode); }
public static class TypeListCache { static Task<TList> GetAsync<TList>(IDataPortal<TList> portal); static void Reset(); } // process lifetime; types change only by migration
public sealed class UserRoleType : CslaType<UserRoleType> { public const int Admin = SystemRoles.Admin; public const int Client = 2; public const int Technical = 3; }
public sealed class ProjectStatusType : CslaType<ProjectStatusType> { public const int Planned = 1, Active = 2, OnHold = 3, Closed = 4; }
public sealed class HasRole : AuthorizationRule { HasRole(AuthorizationActions action, params int[] roleCodes); HasRole(AuthorizationActions action, IMemberInfo element, params int[] roleCodes); }
public interface ITypeDal { Task<IReadOnlyList<TypeDto>> FetchAsync(string typeName, CancellationToken ct); } // "UserRoleType" | "ProjectStatusType"
```

- [ ] **Step 1: Write the failing tests.**
  - `CslaType_Description_FollowsUICulture`
  - `TypeList_ActiveOrCurrent_KeepsRetiredCurrentValue`
  - `HasRole_AllowsListedRole_DeniesOthers` (`AuthorizationRuleTester`)
  - `HasRole_Property_OnlyOneRulePerPropertyAction`: registering two rules for the same (property, action) throws, so all roles go in one rule.
  - `RuleInspector_FindsRequiredAndMaxLength`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - `RuleInspector` parses `GetRuleDescriptions()` URIs case-insensitively and caches per (type, property).
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add business base classes, CslaType lookups and the HasRole rule`.

### Task 3: Users and roles

**Files:**
- Create: `src/CslaStarter.Dal/Users/IUserDal.cs`, `UserDtos.cs`
- Create: `src/CslaStarter.Data/Dal/UserDal.cs`
- Create in `src/CslaStarter.Business/Users/`: `UserEdit.cs`, `UserRoleAssignment.cs`, `UserRoleAssignmentList.cs`, `UserList.cs`, `UserInfo.cs`, `UserProfile.cs`
- Modify: `Labels.resx` / `Labels.fr.resx` and `Messages.resx` / `.fr` (new keys)
- Test: `tests/CslaStarter.Business.Tests/Users/*`, `tests/CslaStarter.Integration.Tests/UserDalTests.cs`

**Rules:**

| Object | Rules |
|---|---|
| `UserEdit` (root) | `DisplayName` required, max 200. `EmployeeNumber` max 20; read and write Admin. `IsActive` and `Roles` write Admin. Object get and edit: Admin. No delete (users are deactivated). |
| `UserRoleAssignmentList` | Child list of role codes; no duplicate codes. |
| `UserProfile` (root) | The signed-in user edits their own `DisplayName` and `PreferredLanguage` (en-CA or fr-CA). Fetching anyone else's profile is denied (per-record rule). |
| `UserList` + `UserInfo` | Read-only, paged; filter by name text and role code. Admin only. |

- [ ] **Step 1: Write the failing tests.**
  - Business tests, with a fake `IUserDal`:
    - `UserEdit_DisplayNameRequired_InBothLanguages`
    - `UserEdit_EmployeeNumber_HiddenFromNonAdmin`
    - `UserEdit_NonAdmin_CannotEditRoles`
    - `UserRoleAssignments_RejectDuplicateRole`
    - `UserProfile_OtherUsersProfile_IsDenied`
    - `UserProfile_PreferredLanguage_MustBeSupported`
  - Integration tests:
    - `UserDal_UpdateRoles_ReplacesAssignments_AndTouchesRoot`
    - `UserDal_StaleVersion_ThrowsConcurrency`
    - `UserDal_List_FiltersByRoleAndPages`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - DTOs are records.
  - `UserDal` maps entities to DTOs, sets the original `Version` for the concurrency check, replaces role rows by key, and throws `Dal.ConcurrencyException` and `NotFoundException`.
  - After a role change, `UserDal` calls `IUserDirectory.Invalidate` through an `IUserRoleCacheInvalidator` in Dal, which Infrastructure implements.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add user and role management business objects and DAL`.

### Task 4: Projects schema, record-level access and per-aggregate concurrency

**Files:**
- Create in `src/CslaStarter.Data/`:
  - `Entities/{ProjectRow,ProjectTaskRow,ProjectMemberRow,ProjectStatusTypeRow}.cs`, `Configurations/Project*.cs`
  - `Access/RecordScopedAttribute.cs`, `Access/BypassRecordAccessAttribute.cs`
  - `Migrations/*_AddProjectsSample.cs`
- Create: `src/CslaStarter.Dal/Projects/ProjectAccess.cs` (`SeesAllRoles = [Admin, Technical]`)
- Modify: `AppDbContext` (takes optional `IDataAccessContext`; named filter `"RecordAccess"` on project rows), `AuditSaveChangesInterceptor` (keeps a Modified root whose children changed in the same save)
- Test: `tests/CslaStarter.Integration.Tests/ProjectAccessTests.cs`; `tests/CslaStarter.Architecture.Tests/RecordAccessTests.cs`

- [ ] **Step 1: Write the failing tests.**
  - `ProjectDal_Fetch_HiddenProject_ThrowsNotFound`
  - `ProjectDal_List_CountsOnlyVisibleProjects`
  - `ProjectDal_AdminAndTechnical_SeeAllProjects`
  - `ProjectDal_ChildChangedByOtherUser_RootSaveFails`
  - `Audit_ChildOnlyChange_TouchesRootVersion`
  - `RecordAccess_IgnoreQueryFilters_OnlyInBypassCode`: a text scan of `src/` that only allows the call inside a file containing `[BypassRecordAccess]`.
  - `RecordScopedEntities_HaveTheFilter`: every `[RecordScoped]` entity type in the EF model has a declared query filter named `RecordAccess`.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - **Named filter:** `HasQueryFilter("RecordAccess", p => _access == null || SeesAll || p.Members.Any(m => m.UserId == _access.UserId))`. `SeesAll` is computed from `_access.RoleCodes`; task and member rows filter through their project.
  - **`DalHelpers.TouchRoot(context, root)`:** sets the root's state to Modified (the version check) and stamps `ModifiedOn`. The interceptor keeps it Modified when a child `IHasAuditRoot` change points at it.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add projects schema with record-level access and per-aggregate concurrency`.

### Task 5: Project business objects and DAL

**Files:**
- Create: `src/CslaStarter.Dal/Projects/IProjectDal.cs`, `ProjectDtos.cs`, `PagedResult.cs`
- Create: `src/CslaStarter.Data/Dal/ProjectDal.cs`
- Create in `src/CslaStarter.Business/Projects/`: `ProjectEdit.cs`, `ProjectTask.cs`, `ProjectTaskList.cs`, `ProjectMember.cs`, `ProjectMemberList.cs`, `ProjectList.cs`, `ProjectInfo.cs`, `ProjectListCriteria.cs`, `CloseProjectCommand.cs`, `Rules/UniqueProjectCode.cs`, `Rules/EndAfterStart.cs`
- Test: `tests/CslaStarter.Business.Tests/Projects/*`, `tests/CslaStarter.Integration.Tests/ProjectDalTests.cs`

**Rules:**

| Object | Rules |
|---|---|
| `ProjectEdit` | `Code`: required, max 20, `^[A-Z0-9-]+$`, unique (async rule through `IProjectDal.IsCodeUniqueAsync`). `Name`: required, max 200. `Description`: max 2000. `StatusCode`: required and an active `ProjectStatusType`. `Budget`: ≥ 0; read Admin and Technical, write Admin. `EndDate` ≥ `StartDate` (error). |
| Object authorization | Create and edit: Admin, Technical. Delete: Admin. Get: any signed-in user (visibility is the record filter). |
| `ProjectTask` | `Title` required, max 200. Write: Admin, Technical. |
| `ProjectMember` | Admin manages members. |
| `ProjectList` | Read-only, paged, sorted by Code, Name or Status, filtered by status and search text. `ProjectInfo.Budget` follows the same read rule. |
| `CloseProjectCommand(projectId)` | Admin, or Technical members of the project. Fails with `Messages.Project_CloseWithOpenTasks` when any task is open. |

- [ ] **Step 1: Write the failing tests.**
  - Business tests, with a fake `IProjectDal`:
    - `ProjectEdit_CodeMustBeUnique`: an async rule shows a "Checking…" busy state, then the error.
    - `ProjectEdit_EndBeforeStart_IsError`
    - `ProjectEdit_Client_CannotReadBudget`
    - `ProjectEdit_Technical_CannotWriteBudget`
    - `ProjectEdit_Client_CannotEdit`
    - `CloseProject_WithOpenTasks_Fails`
    - `CloseProject_TechnicalNonMember_IsDenied`
  - Integration tests:
    - `ProjectDal_Insert_WithTasksAndMembers`
    - `ProjectDal_Update_AddsEditsAndRemovesChildrenByKey`
    - `ProjectDal_List_SortsFiltersAndPages`
    - `ProjectDal_IsCodeUnique_IgnoresSelf`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - Children are matched by key on update.
  - The command runs as a data portal `[Execute]`.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add project sample business objects, rules and DAL`.

### Task 6: Business convention tests

**Files:**
- Create in `tests/CslaStarter.Architecture.Tests/`: `BusinessLayoutTests.cs` (Roslyn), `BusinessPropertyTests.cs`
- Create: `tests/CslaStarter.Integration.Tests/TypeSeedConsistencyTests.cs`

- [ ] **Step 1: Write the tests.**
  - `BusinessClasses_FollowTheLayoutOrder`: for each class in `src/CslaStarter.Business` that derives from a CSLA or App base, member groups appear in the order const codes → registrations → properties → `AddBusinessRules` → `AddObjectAuthorizationRules` → data portal operations.
  - `RegisteredProperties_HaveLocalizedDisplayName`: reflection over Business, for every public static `IPropertyInfo` field's property.
  - `LocalizedDisplayNameKeys_ExistInLabels`
  - `TypeConstants_MatchSeededRows`: every `public const int` in each `CslaType` subclass matches a row in its table, and every row matches a constant, in both directions.
- [ ] **Step 2: Prove they bite.** Swap two member groups in one class, and remove one attribute. Each relevant test fails. Revert.
- [ ] **Step 3: Run all tests.** Expected: PASS.
- [ ] **Step 4: Commit** with the message `DEV-38 Enforce business class layout, localized names and type seeds`.

### Task 7: Documentation and recipes

**Files:** `docs/claude/business-objects.md`, `docs/claude/recipes/add-a-feature.md`, `docs/claude/recipes/add-a-type.md`, `docs/claude/recipes/remove-sample.md`; updates to the root `CLAUDE.md`, `src/CslaStarter.{Business,Dal,Data}/CLAUDE.md`, `tests/CslaStarter.Business.Tests/CLAUDE.md` and `docs/claude/testing.md`.

- [ ] **Step 1: Write the guides and recipes** from the code that exists: exact base classes, rule helpers, DAL patterns and test patterns.
- [ ] **Step 2: Update the CLAUDE.md files.** Run `dotnet test`. Expected: all pass, including the doc link checks.
- [ ] **Step 3: Commit** with the message `DEV-38 Document business objects and add feature recipes`.
