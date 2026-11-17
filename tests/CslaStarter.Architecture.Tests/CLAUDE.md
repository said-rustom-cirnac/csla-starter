# CslaStarter.Architecture.Tests

Checks that keep the solution's shape: the project graph, the boundaries between projects and between `Platform` and `Features`, the `.razor` conventions, the design-system assets, the workflows, the template, and the `CLAUDE.md` files and their links (skipped when `.claude/` is absent).

- References the three `src` projects so it can read their assemblies; nothing references it.
- `RepoPaths.Root` finds the repository by `CslaStarter.slnx`. Use `RepoPaths.Relative(path)` in failure messages.
- A failing check means the code broke a rule. Fix the code, not the test.
- Run: `dotnet test --project tests/CslaStarter.Architecture.Tests`.
