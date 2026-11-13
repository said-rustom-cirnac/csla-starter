# Testing

## Running tests

- `dotnet test` runs every test project. The projects use xUnit v3 on Microsoft Testing Platform, selected in `global.json`.
- Run one class: `dotnet test --filter-class "*ClaudeDocsTests"`.
- Run one method: `dotnet test --filter-method "*Environment_SupportsCanadianCultures"`.
- Shell script tests: `bash scripts/tests/run.sh`. It runs every `test_*` function in `scripts/tests/*.test.sh`, each in its own shell.

## Rules

- A new test project needs at least one test before it's added to the solution. Microsoft Testing Platform fails a run with zero tests.
- Test projects reference `xunit.v3` with no version (the version is set in `Directory.Packages.props`) and add `<Using Include="Xunit" />`.
- Watch every new test fail before writing the code that makes it pass.
- When a test depends on something only some checkouts have, such as the Claude guides, skip it with `Assert.SkipUnless(condition, reason)`. Don't silently pass.

## What each test project covers

| Project | Covers |
|---|---|
| `CslaStarter.Architecture.Tests` | Project reference graph, forbidden packages, razor file pairs, en-CA/fr-CA culture support, resource completeness, mock sign-in config, `CLAUDE.md` coverage and guide links |
| `CslaStarter.Business.Tests` | Business objects with `CslaTestHost` and fake DALs: every rule, role × permission, the save round trip, commands, `LocalizedDisplayName` |
| `CslaStarter.Integration.Tests` | Real PostgreSQL 18 (Testcontainers): schema, roles and grants, audit capture and append-only tables, security events, Data Protection keys, sign-in and roles, bootstrap-admin, the web host (headers, CSP, culture, health), session timeout |

Later phases add `Components.Tests` (bUnit) and `E2E.Tests` (Playwright with the mock Entra ID). Add each one to this table when it's created.

## Business test helpers

- `CslaTestHost.Create(o => o.AsPrincipal(TestPrincipals.Admin).ConfigureCsla(c => c.UseCslaStarterRules()).ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IXDal>(fake)))` builds a CSLA host configured like the app. Fetch with `host.GetDataPortal<T>()`, and keep the host alive while you use the objects it returned. `Integration.Tests/BusinessEndToEndTests` does the same over the real DALs and PostgreSQL.
- `TestPrincipals.Admin`, `.Technical` and `.Client` are principals shaped like the claims transformation's output.
- `AuthorizationRuleTester.For(rule).ForType<T>().AsUser(name, roles…)` tests one authorization rule.
- To see an async rule's busy state, gate the fake DAL with a `TaskCompletionSource`. Then call `await obj.WaitForIdle(TimeSpan)`.

## Integration test helpers

- `TestDatabase.CreateAsync(postgres)` gives each test a fresh copy of a migrated template database. `CreateContext()` connects as `app_runtime`; `asOwner: true` connects as `app_owner`.
- `WebAppFactory(db)` hosts the real app on that database, in the `Testing` environment, with DI scope validation on. Requests are anonymous unless they send `X-Test-User: <name>` (`TestAuthHandler`).
- Use `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) for anything time-based.
