# Plan 1: Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the empty folder into a git repository with the `main`/`claude` branch model. It must have:

- a compiling .NET 10 solution skeleton;
- build rules that fail on any warning or vulnerable package;
- architecture tests that lock in the layer boundaries;
- CI, CodeQL and Dependabot;
- an automatic `claude`→`main` sync that strips Claude files;
- the Claude documentation skeleton;
- GitHub rulesets that protect both branches.

**Architecture:**

- `main` holds single-parent snapshot commits of `claude` minus the Claude-only paths. The snapshots are built by `scripts/sync-main.sh`, and `.github/claude-only-paths.txt` is the single list of those paths.
- The solution has the 7 `src` projects from spec §5. The only test project for now is `CslaStarter.Architecture.Tests`; the other test projects are created by the plans that first give them tests, because an empty test project fails under Microsoft Testing Platform.

**Tech Stack:** .NET SDK 10.0.115, C# with nullable reference types, xUnit v3 4.0.3 on Microsoft Testing Platform, Central Package Management, GitHub Actions, CodeQL, Dependabot, bash.

**Spec:** `docs/superpowers/specs/2026-11-12-csla-starter-design.md` (sections 3, 5, 16, 17, 18, 19). Roadmap: `docs/superpowers/plans/2026-11-12-roadmap.md`.

## Global Constraints

- **Runtime and packages:**
  - Target framework `net10.0`. Microsoft.* packages are pinned to 10.0.x (10.0.15); never take 11.x.
  - CSLA packages 10.2.0. Test framework `xunit.v3` 4.0.3.
- **Build settings** (`Directory.Build.props`):
  - `Nullable` enable, `TreatWarningsAsErrors` true, `AnalysisLevel` latest-recommended.
  - `NuGetAudit` true with `NuGetAuditMode` all, so a vulnerable package fails the build.
  - Central Package Management with lock files (`RestorePackagesWithLockFile`); CI restores with `--locked-mode`. Package source mapping to nuget.org only.
  - `InvariantGlobalization` must stay **false**, because en-CA and fr-CA are required.
- **Allowed project references** (spec §5):

  ```
  Core           → (none)
  Dal            → Core
  Business       → Core, Dal
  Data           → Core, Dal
  Infrastructure → Core, Data
  Components     → Core, Business
  Web            → all
  ```
- **Claude-only paths**, never on `main`: `.claude/`, `**/CLAUDE.md`, `docs/claude/`, `docs/superpowers/`, `.impeccable/`.
- **Branches:** `DEV-<n>-<slug>` cut from `claude`, merged into `claude` by PR. Nobody commits to `main` directly. Every commit message starts with `DEV-38`.
- **Razor files:**
  - Every component has `X.razor` (markup) and `X.razor.cs` (code).
  - No `@code` or `@functions` blocks in any `.razor` file.
- **Remote:** `https://github.com/said-rustom-cirnac/csla-starter` (public, empty, default branch `main`).

## Review Focus

1. **A file deleted on `claude` must also disappear from `main` on the next sync.** A snapshot that only adds or changes files would leave stale code on `main`. *Pinned by* `test_sync_removes_files_deleted_on_claude` in Task 4.
2. **`CLAUDE.md` at any depth must be stripped, but similarly named files must not be.** `src/X/CLAUDE.md` and `tests/Y/CLAUDE.md` go; `CLAUDE.md.txt`, `NOTCLAUDE.md` and `docs/claude-notes.md` stay. *Pinned by* `test_sync_strips_claude_paths_at_any_depth` and `test_sync_keeps_lookalike_names` in Task 4.
3. **Re-running the sync with nothing new must not create an empty commit on `main`.** The workflow can run again after a CI re-run. *Pinned by* `test_sync_is_idempotent` in Task 4.
4. **Dependabot PRs must target `claude`, not the default branch `main`.** Otherwise they'd go around the whole flow. *Pinned by* the `target-branch` check in Task 5, Step 3.
5. **A machine or container without ICU would silently fall back to invariant culture and break French.** *Pinned by* `Environment_SupportsCanadianCultures` in Task 3.

---

### Task 1: Git repository and branch layout

**Files:**
- Create: `.gitignore`, `.gitattributes`, `README.md`
- Already present: `CLAUDE.md`, `docs/architecture/csla-starter-architecture.html`, `docs/superpowers/specs/2026-11-12-csla-starter-design.md`, `docs/superpowers/plans/*.md`, `.impeccable/config.json`

**Interfaces:**
- Produces: branches `main` (root commit, no Claude files), `claude` (from `main`, plus the Claude files) and `DEV-38-foundation` (from `claude`), all pushed to `origin`.

- [ ] **Step 1: Write `.gitignore`.**
  - Start from `dotnet new gitignore`.
  - Append `.impeccable/hook.cache.json`, `**/TestResults/`, `artifacts/`, `node_modules/`, `*.swp`.
  - `.remember/` already ignores itself with its own `*` rule.

- [ ] **Step 2: Write `.gitattributes`.**

```
* text=auto eol=lf
*.cmd text eol=crlf
*.ps1 text eol=crlf
*.png binary
*.woff binary
*.woff2 binary
*.ttf binary
```

- [ ] **Step 3: Write `README.md`.**
  - Contents: the project name, one paragraph of purpose (from spec §1), a "Status: foundation in progress" line, and a link to `docs/architecture/csla-starter-architecture.html`.
  - Nothing about Claude.

- [ ] **Step 4: Create `main` with its root commit.**

```bash
cd /home/said/Documents/repos/CSLA-starter
git init -b main
git add .gitignore .gitattributes README.md docs/architecture/csla-starter-architecture.html
git commit -m "DEV-38 Initial commit: README and architecture document"
```

- [ ] **Step 5: Create `claude` with the Claude files.**

```bash
git switch -c claude
git add CLAUDE.md docs/superpowers .impeccable/config.json
git commit -m "DEV-38 Add design spec, plans and Jira epic mapping"
git switch -c DEV-38-foundation
```

- [ ] **Step 6: Verify the layout.**
  - Run: `git ls-tree -r --name-only main`
    - Expected: exactly `.gitattributes`, `.gitignore`, `README.md`, `docs/architecture/csla-starter-architecture.html`.
  - Run: `git status --short`
    - Expected: empty output.

- [ ] **Step 7: Push.** This is an outward action, approved with this plan.

```bash
gh auth setup-git
git remote add origin https://github.com/said-rustom-cirnac/csla-starter.git
git push -u origin main claude DEV-38-foundation
```

  - Expected: three branches created on origin. `gh api repos/said-rustom-cirnac/csla-starter --jq .default_branch` prints `main`.

---

### Task 2: Build settings and solution skeleton

**Files:**
- Create: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `.editorconfig`, `CslaStarter.slnx`
- Create: `src/CslaStarter.{Core,Dal,Business,Data,Infrastructure}/CslaStarter.*.csproj` (class libraries), `src/CslaStarter.Components/CslaStarter.Components.csproj` (Razor class library), `src/CslaStarter.Web/` (Blazor Web App)

**Interfaces:**
- Consumes: the `DEV-38-foundation` branch from Task 1.
- Produces:
  - `CslaStarter.slnx` at the repo root. Task 3 finds the root by this file.
  - The project reference graph exactly as in Global Constraints.
  - Every `PackageVersion` from spec §19 in `Directory.Packages.props`.

- [ ] **Step 1: Write `global.json`.**

```json
{
  "sdk": { "version": "10.0.100", "rollForward": "latestFeature", "allowPrerelease": false },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

- [ ] **Step 2: Write `Directory.Build.props`.**
  - Properties:
    - `TargetFramework` net10.0, `Nullable` enable, `ImplicitUsings` enable, `LangVersion` latest.
    - `TreatWarningsAsErrors` true, `AnalysisLevel` latest-recommended, `EnforceCodeStyleInBuild` true, `GenerateDocumentationFile` true with `NoWarn` CS1591.
    - `NuGetAudit` true, `NuGetAuditMode` all, `NuGetAuditLevel` low, `RestorePackagesWithLockFile` true.
    - `Deterministic` true, and `ContinuousIntegrationBuild` true when `$(GITHUB_ACTIONS)` is `true`.
    - `InvariantGlobalization` false.

- [ ] **Step 3: Write `Directory.Packages.props`.**
  - Set `ManagePackageVersionsCentrally` true and `CentralPackageTransitivePinningEnabled` true.
  - Add a `PackageVersion` for every package named explicitly in spec §19, with the exact versions.
  - Skip the wildcard rows (`OpenTelemetry.*`, `Microsoft.AspNetCore.*` / `Microsoft.Extensions.*`). Each later plan adds the specific packages it uses, at the version in that row.

- [ ] **Step 4: Write `nuget.config`.**
  - `<clear/>`, one source `nuget.org` = `https://api.nuget.org/v3/index.json`.
  - `packageSourceMapping` with `nuget.org` → pattern `*`.

- [ ] **Step 5: Write `.editorconfig`.**
  - Settings: `root = true`; C# file-scoped namespaces (warning); `var` when the type is apparent; 4-space indent for C#, 2 for json, yml and xml.
  - Naming rules: `_camelCase` private fields, PascalCase constants.
  - `dotnet_diagnostic.CA1707.severity = none` for test projects only (`[tests/**.cs]`), so test names can use underscores.

- [ ] **Step 6: Create the projects.**

```bash
dotnet new sln -n CslaStarter            # .NET 10 SDK writes CslaStarter.slnx
for p in Core Dal Business Data Infrastructure; do dotnet new classlib -o src/CslaStarter.$p; done
dotnet new razorclasslib -o src/CslaStarter.Components
dotnet new blazor -o src/CslaStarter.Web --interactivity Server --all-interactive --empty
dotnet sln add src/*/*.csproj
```

  - Delete the template samples: `Class1.cs` files, plus `Component1.razor`, `ExampleJsInterop.cs` and `wwwroot/exampleJsInterop.js` in Components.
  - Remove `TargetFramework`, `Nullable` and `ImplicitUsings` from each csproj, since they now come from `Directory.Build.props`.

- [ ] **Step 7: Add the project references.**
  - Use `dotnet add <proj> reference <proj>`, following the Global Constraints graph exactly.
  - Web references all six other projects.

- [ ] **Step 8: Turn off prerendering globally (spec D15).**
  - In `src/CslaStarter.Web/Components/App.razor`, set `@rendermode="new InteractiveServerRenderMode(prerender: false)"` on `<Routes>` and `<HeadOutlet>`.
  - Move any `@code` block in the Web template's `.razor` files into a sibling `.razor.cs` partial class.

- [ ] **Step 9: Build.**
  - Run: `dotnet restore && dotnet build -c Release`
  - Expected: `Build succeeded`, 0 warnings, 0 errors, and a `packages.lock.json` next to every csproj.

- [ ] **Step 10: Run the Web app.**
  - Run `dotnet run --project src/CslaStarter.Web` in the background, then `curl -s -o /dev/null -w '%{http_code}' http://localhost:<port>/`.
  - Expected: `200`. Stop the app afterwards.

- [ ] **Step 11: Commit.**

```bash
git add -A
git commit -m "DEV-38 Add solution skeleton, central packages and build rules"
```

---

### Task 3: Architecture tests

**Files:**
- Create: `tests/CslaStarter.Architecture.Tests/CslaStarter.Architecture.Tests.csproj` (package `xunit.v3`, `IsPackable` false, references none of the `src` projects)
- Create: `tests/CslaStarter.Architecture.Tests/RepoPaths.cs`, `ProjectGraphTests.cs`, `RazorConventionTests.cs`, `EnvironmentTests.cs`

**Interfaces:**
- Consumes: `CslaStarter.slnx` and the csproj files from Task 2.
- Produces:
  - `public static class RepoPaths { public static string Root { get; } }`: walks up from `AppContext.BaseDirectory` to the folder containing `CslaStarter.slnx`, and throws `InvalidOperationException` if none is found.
  - `public static string Src => Path.Combine(Root, "src")` and `public static string Tests => Path.Combine(Root, "tests")`.
  - Later plans add convention tests to this project.

- [ ] **Step 1: Write the failing tests.**

```csharp
// ProjectGraphTests.cs
public static readonly TheoryData<string, string[]> Allowed = new()
{
    { "CslaStarter.Core", [] },
    { "CslaStarter.Dal", ["CslaStarter.Core"] },
    { "CslaStarter.Business", ["CslaStarter.Core", "CslaStarter.Dal"] },
    { "CslaStarter.Data", ["CslaStarter.Core", "CslaStarter.Dal"] },
    { "CslaStarter.Infrastructure", ["CslaStarter.Core", "CslaStarter.Data"] },
    { "CslaStarter.Components", ["CslaStarter.Business", "CslaStarter.Core"] },
    { "CslaStarter.Web", ["CslaStarter.Business", "CslaStarter.Components", "CslaStarter.Core",
                          "CslaStarter.Dal", "CslaStarter.Data", "CslaStarter.Infrastructure"] },
};

[Theory, MemberData(nameof(Allowed))]
public void ProjectReferences_MatchAllowedGraph(string project, string[] expected)
    => Assert.Equal(expected.Order(), ProjectFile.Load(project).ProjectReferences.Order());

[Fact]
public void EverySrcProject_IsCoveredByTheGraph()   // a new src project must be added to Allowed
    => Assert.Equal(Allowed.Select(r => (string)r[0]).Order(), ProjectFile.AllSrcProjectNames().Order());

[Theory]
[InlineData("CslaStarter.Core")] [InlineData("CslaStarter.Dal")]
[InlineData("CslaStarter.Business")] [InlineData("CslaStarter.Components")]
public void UpperLayers_DoNotReferenceDataAccessPackages(string project)
    => Assert.DoesNotContain(ProjectFile.Load(project).PackageReferences,
         p => p.StartsWith("Microsoft.EntityFrameworkCore") || p.StartsWith("Npgsql"));

[Fact]
public void Core_ReferencesOnlyExtensionAbstractions()
    => Assert.All(ProjectFile.Load("CslaStarter.Core").PackageReferences,
         p => Assert.Matches(@"^Microsoft\.Extensions\.[A-Za-z.]*Abstractions$", p));

// RazorConventionTests.cs
[Fact] public void RazorFiles_HaveNoCodeBlocks()            // no "@code" or "@functions" in src/**/*.razor
[Fact] public void ComponentRazorFiles_HaveCodeBehind()     // every src/CslaStarter.Components/**/*.razor except _Imports.razor has a sibling .razor.cs

// EnvironmentTests.cs
[Fact]
public void Environment_SupportsCanadianCultures()
{
    var fr = CultureInfo.GetCultureInfo("fr-CA");   // throws in invariant mode
    Assert.Equal(",", fr.NumberFormat.NumberDecimalSeparator);
    Assert.Contains("novembre", new DateTime(2026, 11, 12).ToString("D", fr));
    Assert.Equal(".", CultureInfo.GetCultureInfo("en-CA").NumberFormat.NumberDecimalSeparator);
}
```

  - `ProjectFile` (test helper in the same project) is an XML read of `src/<name>/<name>.csproj`.
  - `ProjectReferences` holds the referenced file names without the extension. `PackageReferences` holds the `Include` values.
  - The two razor tests fail with a message listing every offending file path, relative to `RepoPaths.Root`.

- [ ] **Step 2: Add the project to the solution.**
  - Run `dotnet sln add tests/CslaStarter.Architecture.Tests`, then `dotnet test`.
  - Expected: FAIL to compile, because `RepoPaths` and `ProjectFile` are missing.

- [ ] **Step 3: Implement `RepoPaths` and `ProjectFile`.**
  - Signatures:
    - `static ProjectFile Load(string projectName)`
    - `static IReadOnlyList<string> AllSrcProjectNames()`
    - `IReadOnlyList<string> ProjectReferences`
    - `IReadOnlyList<string> PackageReferences`

- [ ] **Step 4: Run the tests.**
  - Run: `dotnet test`
  - Expected: PASS, 15 or more tests and 0 failed. If a graph test fails, fix the csproj references, not the test.

- [ ] **Step 5: Prove the guards bite.**
  - Temporarily add a reference from Components to Data and run `dotnet test`. `ProjectReferences_MatchAllowedGraph(CslaStarter.Components…)` must fail.
  - Temporarily add `@code {}` to `src/CslaStarter.Web/Components/Routes.razor`. `RazorFiles_HaveNoCodeBlocks` must fail.
  - Revert both, then `git diff --stat src/` shows nothing.

- [ ] **Step 6: Commit.**

```bash
git add -A
git commit -m "DEV-38 Add architecture tests for layer boundaries, razor pairs and cultures"
```

---

### Task 4: Claude-path list, sync script and guard script

**Files:**
- Create: `.github/claude-only-paths.txt`, `scripts/claude-paths.sh` (sourced helper), `scripts/sync-main.sh`, `scripts/check-no-claude-files.sh`
- Test: `scripts/tests/sync-main.test.sh`, `scripts/tests/run.sh`

**Interfaces:**
- Produces:
  - **`.github/claude-only-paths.txt`:** one pattern per line; `#` starts a comment.
    - `dir/` matches that directory prefix from the repo root.
    - `**/NAME` matches a file whose basename is exactly `NAME`, at any depth.
  - **`scripts/claude-paths.sh`** (sourced helper):
    - `claude_paths_filter`: reads paths on stdin and prints the Claude-only ones.
    - It reads its patterns from the file named by `CLAUDE_PATHS_FILE`, defaulting to `.github/claude-only-paths.txt` in the repo root.
  - **`scripts/sync-main.sh <source-ref> <target-ref>`:**
    - Builds the target's next commit, with parent `<target-ref>` and tree = `<source-ref>`'s tree minus the Claude paths.
    - Prints the new commit SHA, or `up-to-date` (exit 0) when that tree equals the target's tree.
    - It doesn't push; the workflow does.
    - Commit message: subject `Sync from claude <short-sha>`, a body listing the subjects of the `claude` commits since the previous sync (`git log --format='- %s' <prev>..<source>`), and the trailer `Source-Commit: <full-sha>`.
    - `<prev>` is read from the target's last `Source-Commit:` trailer. If there's none, use all commits.
  - **`scripts/check-no-claude-files.sh [<ref>]`:**
    - Default ref is `HEAD`.
    - Exit 0 when clean.
    - Exit 1 when any file matches, printing `Claude-only file on <ref>: <path>` for each.

- [ ] **Step 1: Write `.github/claude-only-paths.txt`.**

```
# Paths that exist only on the claude branch. Used by sync-main.sh, check-no-claude-files.sh and the dotnet new template.
.claude/
docs/claude/
docs/superpowers/
.impeccable/
**/CLAUDE.md
```

- [ ] **Step 2: Write the failing tests in `scripts/tests/sync-main.test.sh`.**
  - Each test builds a throwaway repo in `mktemp -d`.
  - `main` starts from a root commit with `README.md`.
  - `claude` branches from it and adds `README.md`, `src/A/a.cs`, `src/A/CLAUDE.md`, `CLAUDE.md`, `tests/B/CLAUDE.md`, `docs/claude/guide.md`, `docs/superpowers/specs/s.md`, `.claude/settings.json`, `.impeccable/config.json`, `docs/claude-notes.md`, `CLAUDE.md.txt` and `src/NOTCLAUDE.md`.
  - The tests:
    - `test_sync_strips_claude_paths_at_any_depth`: after the sync, `git ls-tree -r --name-only <new>` contains none of the 7 Claude paths.
    - `test_sync_keeps_lookalike_names`: the new tree contains `docs/claude-notes.md`, `CLAUDE.md.txt` and `src/NOTCLAUDE.md`.
    - `test_sync_copies_non_claude_files_exactly`: for every non-Claude path, `git rev-parse <new>:<path>` equals `git rev-parse claude:<path>`.
    - `test_sync_removes_files_deleted_on_claude`: delete `src/A/a.cs` on `claude` and sync again. It's absent from the new tree.
    - `test_sync_is_idempotent`: a second sync with no new `claude` commit prints `up-to-date`, and `git rev-list --count main` is unchanged.
    - `test_sync_commit_has_single_parent_and_trailer`: `git rev-list --parents -n1 <new>` shows exactly one parent (the old `main`), and the message contains `Source-Commit: $(git rev-parse claude)`.
    - `test_sync_body_lists_only_new_claude_subjects`: the second sync's body lists only the commits made after the first sync.
    - `test_guard_fails_on_nested_claude_md`: `check-no-claude-files.sh claude` exits 1 and prints `src/A/CLAUDE.md`.
    - `test_guard_passes_on_synced_main`: `check-no-claude-files.sh main` exits 0 after a sync.

  `scripts/tests/run.sh` runs every `scripts/tests/*.test.sh`, prints `PASS`/`FAIL` per test, and exits non-zero if any failed.

- [ ] **Step 3: Run the tests.**
  - Run: `bash scripts/tests/run.sh`
  - Expected: FAIL, because `scripts/sync-main.sh: No such file or directory`.

- [ ] **Step 4: Implement the three scripts.**
  - Approach for `sync-main.sh`:
    - `set -euo pipefail`.
    - Use a temporary index: `GIT_INDEX_FILE=$(mktemp)`, then `git read-tree <source>`.
    - `git ls-files | claude_paths_filter` feeds `git rm --cached -q --pathspec-from-file=-`.
    - `git write-tree`.
    - Compare against `git rev-parse <target>^{tree}`.
    - `git commit-tree <tree> -p <target> -F <message-file>`.
    - Before printing the SHA, run `check-no-claude-files.sh <new-sha>` and exit 2 if it fails.

- [ ] **Step 5: Run the tests.**
  - Run: `bash scripts/tests/run.sh`
  - Expected: 9 PASS, 0 FAIL.

- [ ] **Step 6: Lint the scripts.**
  - Run: `docker run --rm -v "$PWD:/mnt" -w /mnt koalaman/shellcheck:stable scripts/*.sh scripts/tests/*.sh`
  - Expected: no output, exit 0.

- [ ] **Step 7: Commit.**

```bash
git add .github/claude-only-paths.txt scripts
git commit -m "DEV-38 Add claude-to-main snapshot sync and Claude-file guard with tests"
```

---

### Task 5: GitHub workflows and Dependabot

**Files:**
- Create: `.github/workflows/ci.yml`, `.github/workflows/codeql.yml`, `.github/workflows/sync-main.yml`, `.github/workflows/guard-main.yml`, `.github/dependabot.yml`

**Interfaces:**
- Consumes: `scripts/sync-main.sh`, `scripts/check-no-claude-files.sh` and `scripts/tests/run.sh` from Task 4.
- Produces:
  - Workflow `name: CI` with job `build-test`. The Task 7 rulesets require this check by name.
  - The repository secret name `SYNC_DEPLOY_KEY`, created in Task 7.

- [ ] **Step 1: Write `ci.yml`.**
  - `name: CI`.
  - Triggers: `push` to `claude` and `main`, and `pull_request` to `claude`.
  - Top-level `permissions: contents: read`.
  - Job `build-test` on `ubuntu-latest`:
    - checkout
    - `actions/setup-dotnet` with `global-json-file: global.json`
    - `dotnet restore --locked-mode`
    - `dotnet format --verify-no-changes --no-restore`
    - `dotnet build -c Release --no-restore`
    - `dotnet test -c Release --no-build`
    - `bash scripts/tests/run.sh`
  - Pin every action to a full commit SHA with a `# vX.Y.Z` comment. Find SHAs with `gh api repos/<owner>/<action>/git/ref/tags/<tag> --jq .object.sha`.

- [ ] **Step 2: Write `codeql.yml`.**
  - Triggers: `push` and `pull_request` on `claude` and `main`, plus `schedule: cron '23 4 * * 1'`.
  - Permissions: `security-events: write`, `contents: read`.
  - `github/codeql-action/init` with `languages: csharp`, `build-mode: manual`, then a `dotnet build -c Release` step, then `analyze`. Actions pinned to SHAs.

- [ ] **Step 3: Write `dependabot.yml`.**
  - Two update entries, `nuget` (directory `/`) and `github-actions` (directory `/`).
  - Each has `schedule.interval: weekly` and **`target-branch: claude`**.
  - Commit message prefix `DEV-38`.
  - Group minor and patch updates per ecosystem.
  - Verify the target branch with:

```bash
python3 -c "import yaml,sys; d=yaml.safe_load(open('.github/dependabot.yml')); assert all(u['target-branch']=='claude' for u in d['updates']), 'target-branch'; print('ok')"
```
    Expected: `ok`.

- [ ] **Step 4: Write `sync-main.yml`.**
  - Trigger: `on: workflow_run: { workflows: [CI], types: [completed], branches: [claude] }`.
  - Job `if: github.event.workflow_run.conclusion == 'success' && github.event.workflow_run.event == 'push'`.
  - `concurrency: { group: sync-main, cancel-in-progress: false }`, `permissions: contents: read`.
  - Steps:
    - checkout with `ssh-key: ${{ secrets.SYNC_DEPLOY_KEY }}`, `fetch-depth: 0`, `ref: main`
    - `git fetch origin claude`
    - `sha=$(bash scripts/sync-main.sh ${{ github.event.workflow_run.head_sha }} origin/main)`
    - if `$sha` is not `up-to-date`, `git push origin "$sha:refs/heads/main"`
  - Pass the `head_sha` (not the branch tip), so a commit that failed CI never syncs.

- [ ] **Step 5: Write `guard-main.yml`.**
  - Trigger: `push` to `main`.
  - Job `guard` runs `bash scripts/check-no-claude-files.sh HEAD`. If it fails, the job fails and shows the offending paths.

- [ ] **Step 6: Lint the workflows.**
  - Run: `docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint:latest -color`
  - Expected: no findings, exit 0.

- [ ] **Step 7: Commit.**

```bash
git add .github
git commit -m "DEV-38 Add CI, CodeQL, Dependabot, main sync and main guard workflows"
```

---

### Task 6: Claude documentation skeleton

**Files:**
- Modify: `CLAUDE.md` (keep line 3, `Jira project epic: DEV-37 (CSLA-starter)`)
- Create: `src/CslaStarter.{Core,Dal,Business,Data,Infrastructure,Components,Web}/CLAUDE.md`, `tests/CslaStarter.Architecture.Tests/CLAUDE.md`
- Create: `docs/claude/architecture.md`, `docs/claude/conventions.md`, `docs/claude/workflow.md`, `docs/claude/testing.md`, `docs/claude/template-dev.md`
- Create: `.claude/settings.json`
- Test: `tests/CslaStarter.Architecture.Tests/ClaudeDocsTests.cs`

**Interfaces:**
- Consumes: `RepoPaths` from Task 3.
- Produces: the doc layout that later plans extend. Each later plan adds its guide (`business-objects.md`, `data-access.md`, `components.md`, `localization.md`, `security.md`, `realtime.md`, `gcds.md`, `recipes/`) and links it from the root `CLAUDE.md`.

- [ ] **Step 1: Write the failing tests in `ClaudeDocsTests.cs`.**
  - Both tests return early (pass) when `Path.Combine(RepoPaths.Root, ".claude")` doesn't exist. That's the case on `main` and in `--claude false` apps.

```csharp
[Fact] public void EveryProjectFolder_HasClaudeMd()
// every directory under src/ and tests/ that contains a *.csproj also contains CLAUDE.md; failure lists the missing folders

[Fact] public void ClaudeMdLinks_PointToExistingFiles()
// every relative markdown link target in every CLAUDE.md and docs/claude/**/*.md resolves to an existing file; failure lists "<file>: <link>"
```

- [ ] **Step 2: Create `.claude/settings.json`, then run the tests.**
  - Settings:
    - `permissions.allow`: `Bash(dotnet build:*)`, `Bash(dotnet test:*)`, `Bash(dotnet restore:*)`, `Bash(dotnet format:*)`, `Bash(dotnet run:*)`, `Bash(bash scripts/tests/run.sh)`, `Bash(docker compose:*)`, `Bash(git status:*)`, `Bash(git diff:*)`, `Bash(git log:*)`.
    - `permissions.deny`: `Bash(git push origin main:*)`, `Bash(git push --force:*)`.
  - Run: `dotnet test --filter-class *ClaudeDocsTests` (MTP filter syntax).
  - Expected: FAIL, listing all 8 project folders.

- [ ] **Step 3: Write the root `CLAUDE.md`.** Keep it under 120 lines, with these sections in this order:
  - Title and the epic line.
  - **What this is:** two sentences.
  - **Commands:** build, test, script tests, run the Web app.
  - **Architecture map:** the allowed-reference block from Global Constraints, and one line per project.
  - **Golden rules:**
    - CSLA class layout order (registrations, properties, rules, data portal)
    - validation and permissions only in CSLA, never in the UI
    - `obj = await obj.SaveAsync()`
    - `Func<string>` rule messages
    - `[LocalizedDisplayName]` on every business property
    - `.razor` + `.razor.cs`, no `@code`
    - never commit to `main`
    - commit messages start with the Jira key
  - **Workflow:** points to `docs/claude/workflow.md`.
  - **Guides:** links to each `docs/claude/*.md`.
  - **Keeping docs current:** "a change that alters a convention, command or project updates the matching CLAUDE.md and guide in the same commit".

- [ ] **Step 4: Write the per-project `CLAUDE.md` files.** Each one covers:
  - purpose (one paragraph from spec §5);
  - allowed references;
  - the conventions that apply in that project, as bullets from the spec sections that name it;
  - gotchas: for Business, the spec §6.4 CSLA list; for Components, the §12.4 GCDS traps;
  - links to the relevant guide.

- [ ] **Step 5: Write the five guides.**
  - **`architecture.md`:** spec §5 and the D-register summary.
  - **`conventions.md`:** naming, class layout (spec §6.2), razor pairs, nullable rules.
  - **`workflow.md`:** branches, Jira keys, the sync and guard, and how to add a Claude-only path (edit `.github/claude-only-paths.txt`).
  - **`testing.md`:** how to run tests with Microsoft Testing Platform, `--filter-class` examples, and the rule that every new test project needs at least one test.
  - **`template-dev.md`:** notes that apply only to developing this template. The template always excludes this file.

- [ ] **Step 6: Run the tests.**
  - Run: `dotnet test`
  - Expected: all tests PASS, including both `ClaudeDocsTests`.

- [ ] **Step 7: Commit.**

```bash
git add CLAUDE.md src/*/CLAUDE.md tests/*/CLAUDE.md docs/claude .claude tests/CslaStarter.Architecture.Tests
git commit -m "DEV-38 Add Claude documentation skeleton with link and coverage tests"
```

---

### Task 7: GitHub settings and the first pull request

**Files:** none. This task changes repository settings through `gh api`.

**Interfaces:**
- Consumes:
  - The workflows from Task 5 (check name `build-test`, secret name `SYNC_DEPLOY_KEY`).
  - The branches from Task 1.
- Produces:
  - Rulesets `protect-main` and `protect-claude`.
  - A deploy key titled `sync-main`.
  - Dependabot security updates turned on.
  - An open PR from `DEV-38-foundation` into `claude`.

- [ ] **Step 1: Confirm with Said before changing settings.**
  - List the four changes below. Proceed only on his yes.

- [ ] **Step 2: Create the sync deploy key.**
  - `ssh-keygen -t ed25519 -N '' -C sync-main -f <scratchpad>/sync_main_key`
  - `gh repo deploy-key add <scratchpad>/sync_main_key.pub --allow-write --title sync-main`
  - `gh secret set SYNC_DEPLOY_KEY < <scratchpad>/sync_main_key`
  - Delete both key files.
  - Expected: `gh repo deploy-key list` shows `sync-main` (read-write), and `gh secret list` shows `SYNC_DEPLOY_KEY`.

- [ ] **Step 3: Create the rulesets** with `gh api -X POST repos/said-rustom-cirnac/csla-starter/rulesets`.
  - **`protect-main`:**
    - target `branch`, include `refs/heads/main`
    - rules `deletion`, `non_fast_forward`, `update`
    - `bypass_actors: [{ "actor_type": "DeployKey", "bypass_mode": "always" }]`
  - **`protect-claude`:**
    - target `branch`, include `refs/heads/claude`
    - rules `deletion`, `non_fast_forward`, `pull_request` (0 required approvals, so Said can merge his own PRs), `required_status_checks` with context `build-test`
    - bypass actor: the repository admin role (`actor_type: RepositoryRole`, `actor_id: 5`, `bypass_mode: pull_request`), so an emergency PR can be merged without waiting for checks
  - Expected: `gh api repos/said-rustom-cirnac/csla-starter/rulesets --jq '.[].name'` prints both names.

- [ ] **Step 4: Turn on Dependabot security updates.**
  - Run: `gh api -X PUT repos/said-rustom-cirnac/csla-starter/automated-security-fixes`
  - Expected: `gh api repos/said-rustom-cirnac/csla-starter --jq .security_and_analysis.dependabot_security_updates.status` prints `enabled`.

- [ ] **Step 5: Push and open the PR.**

```bash
git push origin DEV-38-foundation
gh pr create --base claude --head DEV-38-foundation --title "DEV-38 Foundation: repo layout, build rules, CI, sync to main, Claude docs" --body-file <scratchpad>/pr-body.md
```

  - The PR body:
    - one paragraph summary;
    - a checklist of the 7 tasks;
    - `Plan: docs/superpowers/plans/2026-11-12-plan-1-foundation.md`;
    - ends with the attribution line from the session reminder.
  - Bind the PR with the ccd_pr tools.

- [ ] **Step 6: Verify CI on the PR.**
  - Expected: `CI / build-test` and CodeQL complete green.
  - Merging is done in finishing-a-development-branch, and only when Said asks. After the merge:
    - `sync-main` runs;
    - `git ls-tree -r --name-only origin/main | grep -E '(^|/)CLAUDE\.md$|^\.claude/|^docs/(claude|superpowers)/|^\.impeccable/'` prints nothing;
    - `guard-main` is green.
