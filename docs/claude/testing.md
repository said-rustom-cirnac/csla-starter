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
| `CslaStarter.Architecture.Tests` | Project reference graph, forbidden packages, razor file pairs, en-CA/fr-CA culture support, `CLAUDE.md` coverage and guide links |

Later phases add `Business.Tests` (Csla.Testing), `Components.Tests` (bUnit), `Integration.Tests` (Testcontainers PostgreSQL) and `E2E.Tests` (Playwright with a mock Entra ID). Add each one to this table when it's created.
