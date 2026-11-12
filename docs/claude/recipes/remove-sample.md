# Recipe: remove the Projects sample

The Projects feature exists to demonstrate every pattern. Remove it once your app has its own features.

1. **Delete these files:**
   - `src/CslaStarter.Business/Projects/`
   - `src/CslaStarter.Business/Types/ProjectStatusType.cs`
   - `src/CslaStarter.Dal/Projects/`
   - `src/CslaStarter.Data/Dal/ProjectDal.cs`
   - `src/CslaStarter.Data/Entities/Project*.cs`
   - `src/CslaStarter.Data/Configurations/ProjectConfiguration.cs`
2. **Edit these files:**
   - `AppDbContext`: remove the `Projects`, `ProjectTasks` and `ProjectStatusTypes` sets and the two `RecordAccess` filters. Keep the filter constant for your own scoped entities.
   - `TypeDal`: remove the `"ProjectStatusType"` case.
   - `AddCslaStarterData`: remove the `IProjectDal` registration.
3. **Migration:**
   - If `AddProjectsSample` has **never been applied** to a shared database, delete it with `dotnet ef migrations remove` (newest first).
   - Otherwise, add a new migration that drops `project_member`, `project_task`, `project` and `project_status_type`.
4. **Resources:** delete the `Project_*`, `Task_*` and `Member_*` keys from `Labels.resx` and `Messages.resx`, in both languages.
5. **Tests:** delete `tests/CslaStarter.Business.Tests/Projects/`, plus `ProjectAccessTests.cs` and `ProjectDalTests.cs` in Integration.Tests.
6. **`BusinessPropertyTests.ProjectAccess_SeesAllRoles_MatchTheBusinessRoles`:** delete it, or point it at your own access policy.
7. **Run** `dotnet test`.
