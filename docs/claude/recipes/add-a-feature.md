# Recipe: add a feature (an aggregate with children)

Follow the Projects sample, file for file.

1. **Resources:** add `Feature_*` labels to `Core/Resources/Labels.resx` and `Labels.fr.resx`, and any rule messages to `Messages.resx` / `.fr.resx`. Both languages, every key.
2. **Contract (Dal):** under `Dal/<Feature>/`:
   - DTOs: `XDto` (read), `XSaveDto` (whole aggregate), `XInfoDto` (list row), `XListCriteria`.
   - `IXDal`, documenting `NotFoundException` and `ConcurrencyException`.
   - If records are scoped, an `XAccess` with the roles that see everything.
3. **Data:**
   - **Entities:** the root implements `IAuditedEntity` and `IAggregateRoot`; children implement `IHasAuditRoot`. Add `[AuditEntity("X")]` on each, `[AuditIgnore]` on foreign keys to the root, `[AuditTypeCode("XType")]` on lookup codes, `[AuditMask]` on sensitive columns, and `[RecordScoped]` when visibility is limited.
   - **Configuration:**
     - `ValueGeneratedNever()` on Guid keys;
     - `IsRowVersion()` on `Version`;
     - `ClientCascade` from the root to its children (so deletes are audited);
     - `HasQueryFilter(AppDbContext.RecordAccessFilter, …)` in `AppDbContext.OnModelCreating` when scoped. Never filter both sides of a navigation: EF recurses forever.
   - **DAL:** load with `Include`, call `DalHelpers.ExpectVersion`, reconcile the children by key, call `DalHelpers.TouchRoot`, then `DalHelpers.SaveAggregateAsync` (pass your unique indexes so violations become `DuplicateValueException`), and return a fresh fetch. Lists filter, sort, count, then page. Register the DAL in `AddCslaStarterData`.
   - **Migration:** `dotnet ef migrations add Add<Feature> -p src/CslaStarter.Data -s src/CslaStarter.Data -o Migrations`.
4. **Business:** under `Business/<Feature>/`:
   - the root (`AppRootBase<T>`), its children and child lists, the read-only list and its row, commands, and `Rules/`;
   - the layout, permissions and data portal patterns in [business objects](../business-objects.md).
5. **Tests:**
   - Business tests with a fake DAL: every rule, every role × permission, the save round trip.
   - Integration tests for the DAL: insert, update by key, delete with its audit, list paging, hidden records, the cross-user concurrency failure.
6. **Run** `dotnet test`. The convention tests check the layout, the labels and the type seeds.
