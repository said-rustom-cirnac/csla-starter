# Testing

## Running tests

- `dotnet test` runs every test project. The projects use xUnit v3 on Microsoft Testing Platform, selected in `global.json`.
- Run one class: `dotnet test --filter-class "*ClaudeDocsTests"`.
- Run one method: `dotnet test --filter-method "*Environment_SupportsCanadianCultures"`.
- Shell script tests: `bash scripts/tests/run.sh`. It runs every `test_*` function in `scripts/tests/*.test.sh`, each in its own shell.
- **Coverage gate:** CI runs `Business.Tests` and `Components.Tests` again with `--coverage --coverage-output-format cobertura`, and `scripts/check-coverage.sh 80 …` fails the build if `CslaStarter.Business` or `CslaStarter.Components` drops below 80% of lines. Locally: `dotnet test --project tests/CslaStarter.Business.Tests --coverage --coverage-output-format cobertura --coverage-output Business.cobertura.xml --results-directory TestResults/coverage`, then the same check.
- **End-to-end tests** compile on every pull request (CI builds the project, outside the solution) and run in the `E2E` workflow.

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
| `CslaStarter.Components.Tests` | bUnit: every `Csla*` component rendered over real business objects and fake DALs, with the real change hub and presence tracker; plus the pure helpers (diffs, merges, hints, debounce) |
| `CslaStarter.Integration.Tests` | Real PostgreSQL 18 (Testcontainers): schema, roles and grants, audit capture and append-only tables, security events, Data Protection keys, sign-in and roles, bootstrap-admin, the web host (headers, CSP, culture, health), session timeout |
| `CslaStarter.E2E.Tests` | A real browser (Playwright Chromium) against the app on Kestrel with PostgreSQL and the mock Entra ID in containers: sign-in and sign-out, two browsers seeing live updates and presence, a language switch keeping edits, the session timeout, and an axe WCAG 2.1 AA scan of the sample pages. **Opt-in:** outside the solution, it runs with `E2E=1` (see its `CLAUDE.md`); the `E2E` workflow runs it on pushes to `claude` and nightly, apart from CI. |

## Business test helpers

- `CslaTestHost.Create(o => o.AsPrincipal(TestPrincipals.Admin).ConfigureCsla(c => c.UseCslaStarterRules()).ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IXDal>(fake)))` builds a CSLA host configured like the app. Fetch with `host.GetDataPortal<T>()`, and keep the host alive while you use the objects it returned. `Integration.Tests/BusinessEndToEndTests` does the same over the real DALs and PostgreSQL.
- `TestPrincipals.Admin`, `.Technical` and `.Client` are principals shaped like the claims transformation's output.
- `AuthorizationRuleTester.For(rule).ForType<T>().AsUser(name, roles…)` tests one authorization rule.
- To see an async rule's busy state, gate the fake DAL with a `TaskCompletionSource`. Then call `await obj.WaitForIdle(TimeSpan)`.

## Integration test helpers

- `TestDatabase.CreateAsync(postgres)` gives each test a fresh copy of a migrated template database. `CreateContext()` connects as `app_runtime`; `asOwner: true` connects as `app_owner`.
- `WebAppFactory(db)` hosts the real app on that database, in the `Testing` environment, with DI scope validation on. Requests are anonymous unless they send `X-Test-User: <name>` (`TestAuthHandler`).
- Use `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) for anything time-based.
- Dispose the `TestDatabase` in the test class's `DisposeAsync` (`=> _db.DisposeAsync()`): it drops the copy, which would otherwise fill the container's disk.
- `TestDatabase.CreateContext(access, notifier: recorder)` also publishes committed changes to a recording `IEntityChangeNotifier`.
- `Eventually.True(() => condition)` polls instead of sleeping, for anything that crosses the backplane.
