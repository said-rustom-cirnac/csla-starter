# CslaStarter.Integration.Tests

Tests against a real PostgreSQL 18: the migrated schema, the sign-in directory (`UserDirectoryTests`), the text store and seeder (`TextStoreTests`) and the user DAL (`UserDalTests`). They need Docker: Testcontainers starts a `postgres:18` container on a free port and removes it after the run.

- References `CslaStarter.Web`, which brings `Data` and `Domain`.
- **`PostgresFixture`** is an assembly fixture: one container per run. It migrates a template database once, through `AddAppData`, so a migration out of step with the model fails every test.
- **`TestDatabase`** gives each test a clean database. Create it with `TestDatabase.CreateAsync(postgres)` in the test class's `InitializeAsync` and dispose it in `DisposeAsync`. xUnit makes a new class instance for every test, so every test gets its own copy of the template and no test sees another's rows. Disposing drops the copy.
- `_db.Services` resolves what `AddAppData` registers, inside one scope that lasts for the test. The plain-SQL helpers arrange and check rows directly: `TableNamesAsync()`, `CountAsync(table, where)`, `ExecuteAsync(sql, args…)` and `ScalarAsync<T>(sql, args…)`. Each distinct `@name` in the SQL takes the next argument, in the order the names first appear.
- Tests connect as the container's superuser; the app's two database roles are not set up yet.
- Pass `TestContext.Current.CancellationToken` to any method that takes a token: the xUnit analyzer rejects `default`. The test classes keep it in a `Ct` property.
- Copy `UserDirectoryTests.cs` for a new test class.
- Run: `dotnet test --project tests/CslaStarter.Integration.Tests` (one test: add `--filter-method "*Name"`).
