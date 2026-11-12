# CslaStarter.Architecture.Tests

Checks that keep the solution's shape:

- the project reference graph and forbidden packages;
- `.razor` / `.razor.cs` pairs and no `@code` blocks;
- en-CA and fr-CA culture support;
- `CLAUDE.md` coverage and guide links, which are skipped when `.claude/` is absent.

## Rules

- `RepoPaths.Root` finds the repository by `CslaStarter.slnx`. Use `RepoPaths.Src`, `RepoPaths.Tests` and `RepoPaths.Relative(path)` in failure messages.
- Adding a src project? Add it to `AllowedGraph` in `ProjectGraphTests.cs`. `EverySrcProject_IsCoveredByTheGraph` fails until you do.
- A failing check here means the code broke a rule. Fix the code, not the test.

Guide: [testing](../../docs/claude/testing.md).
