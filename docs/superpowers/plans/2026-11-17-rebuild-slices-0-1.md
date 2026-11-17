# Starter Rebuild, Slices 0 and 1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the first build with the three-project skeleton (slice 0), then prove one screen end to end in a real browser: mock sign-in, the layout shell, a Users list and an edit screen with one field, saved to PostgreSQL with audit, in English and French from database text, passing axe (slice 1).

**Architecture:** `Domain` holds CSLA objects, rules, DAL contracts and the translator. `Data` holds EF Core, the unit of work, audit capture and DAL implementations, and never references CSLA. `Web` is the host, the `Csla*` kit and the pages. Each project has `Platform/` (starter-owned) and `Features/` (app-owned).

**Tech stack:** .NET 10, CSLA 10.2.0 with the property generator, EF Core 10 on PostgreSQL 18 (Npgsql), Blazor Server (`prerender: false`), GC Design System 1.7.0 (vendored), xUnit v3 on Microsoft Testing Platform, bUnit, Testcontainers, Playwright with axe.

**Spec:** `docs/superpowers/specs/2026-11-17-csla-starter-rebuild-design.md` (sections in brackets below, for example [§7.2]).

**Jira:** DEV-39. **Branch:** `DEV-39-starter-rebuild`. Every commit message starts with `DEV-39` and ends with the `Co-Authored-By` line the session gives. When execution starts, move DEV-39 to In Progress and add the `implement` line to its Sessions section.

## Global Constraints

- .NET 10; Microsoft.* packages stay on 10.0.x; no 11.x packages. CSLA packages are all 10.2.0. [§4]
- Warnings are errors, CSLA analyzers included. Fix the cause; never suppress. Nullable on. Lock files committed; CI restores with `--locked-mode`. [§5]
- Business classes are `sealed partial`, laid out as properties, then business rules, then data portal operations. Every operation method carries its attribute; no legacy method names (`UseLegacyOperationMethods = false`). [§6.1, §6.2, §6.8]
- Rules and permissions live only in business objects. Components read them and never repeat them. [§3]
- Saving returns a new object: always `obj = await obj.SaveAsync()`. [§6.5]
- `Domain` references no EF Core, Npgsql or ASP.NET Core assembly. The compiled `Data` assembly does not reference `Csla`. Pages and components never use a `Data` type. `Platform` never references `Features`. [§5]
- Business data is written only through `IUnitOfWork.CommitAsync()`. Only `Platform` code calls `SaveChanges`, `IgnoreQueryFilters` or raw SQL. [§5, §7.2]
- No `.resx` files. Interface text is `T("…")` or `[Label("…")]` with a string literal. Error messages are named only by `ErrorMessageType` constants. [§9]
- Every component is `X.razor` (markup only) plus `X.razor.cs`. No `@code` or `@functions`. No inline `style` attribute. Every inline `<script>` carries the CSP nonce. [§8.3, §11.2]
- Design-system fields set `validate-on="other"`; forms set `novalidate`; every field host has an `id`; never bind a changing `class` on a design-system element. [§11.3]
- Test first, for every behaviour: watch the test fail for the right reason, then make it pass. [§15]
- Never commit to `main` or `claude`. All work stays on `DEV-39-starter-rebuild`. [§16]

## Standing documentation step (added 2026-11-17, spec §16.1)

From Task 6 on, every task ends with this step before its commit, and its review checks it:

- Update the `CLAUDE.md` of each project the task changed, and any handbook page or guide the change makes false or incomplete. A new test project brings its own `CLAUDE.md`.
- Say only what is true at that commit. Name real files as the pattern to copy.
- List the documentation changes in the task report, or state that none was needed and why.

## Rulings made while planning

Each is my call, within what the spec leaves to the builder. Change any of them before execution starts.

1. **The first build is the reference, not the base.** Its code is read with `git show claude:<path>` and ported only where a task says so, under a new test.
2. **One migration, `Initial`, until the rebuild merges.** It is regenerated whenever the model changes. No database exists that would need an incremental one.
3. **The design system moves** from `src/CslaStarter.Components/wwwroot/gcds` to `src/CslaStarter.Web/wwwroot/gcds` [§5].
4. **Stale Claude guides and skills are deleted in slice 0** (`docs/claude/*` except `workflow.md` and `template-dev.md`; `.claude/skills/*`). They describe the first build and would mislead. The handbook grows with each slice instead of all at once in slice 8: slices 0 and 1 write `docs/handbook/getting-started.md` and `docs/handbook/architecture.md`.
5. **The end-to-end project stays outside the solution**, as today (Playwright's package is large). CI builds it separately.
   **A test project is created by the task that writes its first test**, because a project with no tests fails the test run. Until Task 13 creates the end-to-end project, its two workflow steps are switched off.
6. **A missing translation is answered at once from the fallback and inserted in the background.** `Translate` is synchronous (attributes call it), so it never waits on the database.
7. **A DAL write returns an `IStagedRow`** whose `Version` is readable after the commit. This is how the new row version reaches the root object [§19].
8. **In slice 1, any signed-in user may list and edit users.** Roles arrive in slice 2, which replaces this rule with Admin only. It is listed under "Carried into slice 2".
9. **CI for this branch runs through a draft pull request into `claude`**, so no workflow needs the branch's name. Opening it needs Said's go-ahead (Task 4).

## Local machine notes (this workstation only; not for CI or the docs)

- The main disk has under 400 MB free. Before any `dotnet` command, in every shell:
  ```bash
  export NUGET_PACKAGES=/tmp/csla-rebuild/nuget ArtifactsPath=/tmp/csla-rebuild/artifacts PLAYWRIGHT_BROWSERS_PATH=/tmp/csla-rebuild/ms-playwright
  ```
- `docker compose` is not installed and port 5432 is taken by another project. Tests use Testcontainers, which needs neither.
- One test: `dotnet test --project tests/<Project> --filter-method "*Name"`. Send long output to a file and read its tail.
- Installing Chromium for Playwright is a download: ask Said before the first install in a new `/tmp`.
- CSLA 10.2 source and docs fetched during design may still be in the session scratchpad under `csla-research/`. Otherwise: `gh api "repos/MarimerLLC/csla/contents/<path>?ref=v10.2.0"`.

## Review Focus

Inputs the spec implies that are easy to miss. Each has a test in the task named.

1. **A display name that is only spaces** must fail "required", the same as empty (Task 6).
2. **A culture that is neither English nor French** (`es-MX`) gets English, never an exception or an empty label (Task 9).
3. **A sign-in token with no `name` claim** still produces a usable display name: `preferred_username`, then the object id (Task 10).
4. **An unknown or malformed user id in the address** shows the not-found page, not the error page (Tasks 12 and 13).
5. **Text asked for before the translator has loaded, or with the database down**, returns the fallback and never throws (Task 9).

---

# Part A — Slice 0: the skeleton

### Task 1: Three projects and one solution

**Files:**
- Delete: `src/CslaStarter.Business`, `src/CslaStarter.Core`, `src/CslaStarter.Dal`, `src/CslaStarter.Infrastructure`, `src/CslaStarter.Components` (after Task 3 moves `wwwroot/gcds`; until then leave that folder only), everything inside `src/CslaStarter.Data` and `src/CslaStarter.Web`, and all five folders under `tests/`.
- Create: `src/CslaStarter.Domain/CslaStarter.Domain.csproj`, `src/CslaStarter.Data/CslaStarter.Data.csproj`, `src/CslaStarter.Web/CslaStarter.Web.csproj`, `src/CslaStarter.Web/Program.cs`
- Create: `tests/CslaStarter.Architecture.Tests/CslaStarter.Architecture.Tests.csproj`, `ProjectGraphTests.cs`; port `ProjectFile.cs`, `RepoPaths.cs`, `SourceFiles.cs` from `claude`
- Modify: `CslaStarter.slnx`, `Directory.Packages.props`, `.editorconfig`

**Interfaces:**
- Produces: marker types `CslaStarter.Domain.DomainAssembly`, `CslaStarter.Data.DataAssembly` (public static classes) and `Program` (public partial) for tests to find each assembly. Folders `Platform/` and `Features/` exist in each `src` project (a `.gitkeep` until code arrives).
- Produces: project references. `Domain` → none. `Data` → `Domain`. `Web` → `Domain`, `Data`. `Architecture.Tests` → all three.
- The other four test projects are created later, each by the task that writes its first test, and added to `CslaStarter.slnx` then (except `E2E.Tests`): `Domain.Tests` → `Domain` (Task 5); `Integration.Tests` → `Web` (Task 7); `Web.Tests` → `Web` (Task 11); `E2E.Tests` → `Web` (Task 13). They follow the first build's shape: `OutputType` Exe, `xunit.v3`, `Using Include="Xunit"`. `Domain.Tests` adds `Csla.Testing` and code coverage; `Web.Tests` adds `bunit` and code coverage; `Integration.Tests` adds Testcontainers, `Microsoft.AspNetCore.Mvc.Testing` and time provider testing; `E2E.Tests` adds Playwright, axe, Testcontainers and Mvc.Testing, and links `deploy/mock-oidc/mock-oidc.json`.

- [ ] **Step 1: Remove the first build's projects and tests** (keep `src/CslaStarter.Components/wwwroot/gcds` in place for Task 3), then create only `tests/CslaStarter.Architecture.Tests` with its ported helpers and this test:

```csharp
public sealed class ProjectGraphTests
{
    [Theory]
    [InlineData("CslaStarter.Domain", new string[0])]
    [InlineData("CslaStarter.Data", new[] { "CslaStarter.Domain" })]
    [InlineData("CslaStarter.Web", new[] { "CslaStarter.Data", "CslaStarter.Domain" })]
    public void Project_References_OnlyItsAllowedProjects(string project, string[] allowed) =>
        Assert.Equal(allowed, ProjectFile.Load(project).ProjectReferences.Order());

    [Fact]
    public void Solution_Lists_OnlyRebuildProjects()
    {
        var projects = ProjectFile.SolutionProjects().ToHashSet();
        Assert.Superset(new HashSet<string> { "CslaStarter.Domain", "CslaStarter.Data", "CslaStarter.Web", "CslaStarter.Architecture.Tests" }, projects);
        Assert.Subset(new HashSet<string> { "CslaStarter.Domain", "CslaStarter.Data", "CslaStarter.Web", "CslaStarter.Architecture.Tests",
            "CslaStarter.Domain.Tests", "CslaStarter.Integration.Tests", "CslaStarter.Web.Tests" }, projects);
    }
}
```

- [ ] **Step 2: Run it.** `dotnet test --project tests/CslaStarter.Architecture.Tests` — Expected: FAIL, the three `src` project files are not found.
- [ ] **Step 3: Create the projects.**
  - `Directory.Packages.props`: add `Csla.Generator.AutoImplementProperties.CSharp` 10.2.0 (and its attributes package if the ProjectTracker sample at tag `v10.2.0` references one); fix the comment to point at spec §4.
  - `Domain`: packages `Csla` and the generator. `Data`: the EF Core, Npgsql, naming-convention and Data Protection packages already pinned. `Web`: `Microsoft.NET.Sdk.Web`, `Csla.AspNetCore`, `Csla.Blazor`, keep `BlazorDisableThrowNavigationException`. `Program.cs` is a minimal host that maps `/health/live`.
  - `CslaStarter.slnx` lists the three `src` projects and `Architecture.Tests`.
  - `.editorconfig`: remove the three `CSLA0007` sections; keep the migrations section.
- [ ] **Step 4: Lock and build.** `dotnet restore --force-evaluate && dotnet build -c Release && dotnet test -c Release --no-build` — Expected: build with 0 warnings; `ProjectGraphTests` 4 passed.
- [ ] **Step 5: Format.** `dotnet format --verify-no-changes` — Expected: exit 0.
- [ ] **Step 6: Commit** `DEV-39 Replace the first build with the three-project skeleton`.

### Task 2: Boundaries are enforced

**Files:**
- Create: `tests/CslaStarter.Architecture.Tests/Boundaries.cs`, `BoundaryTests.cs`

**Interfaces:**
- Produces: `static IReadOnlyList<string> Boundaries.Check(string repoRoot)`. Each entry is `"<relative path>: <rule>"`. Later slices add rules here.

- [ ] **Step 1: Write the failing tests.** `Violation` writes one file under a temporary root and returns `Boundaries.Check(root)`.

```csharp
[Theory]
[InlineData("src/CslaStarter.Domain/Platform/A.cs", "using CslaStarter.Domain.Features.Projects;", "Platform must not use Features")]
[InlineData("src/CslaStarter.Web/Platform/Pages/A.razor.cs", "using CslaStarter.Web.Features.Projects;", "Platform must not use Features")]
[InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "await db.SaveChangesAsync();", "only Platform may call SaveChanges")]
[InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "query.IgnoreQueryFilters()", "only Platform may call IgnoreQueryFilters")]
[InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "db.Database.ExecuteSqlRaw(\"x\")", "only Platform may run raw SQL")]
[InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "using CslaStarter.Data.Features.Contracts;", "a feature must not use another feature's data")]
[InlineData("src/CslaStarter.Web/Features/Projects/P.razor.cs", "using CslaStarter.Data;", "pages and components must not use Data")]
[InlineData("src/CslaStarter.Web/Platform/Components/C.razor.cs", "using CslaStarter.Data.Platform;", "pages and components must not use Data")]
[InlineData("src/CslaStarter.Web/Platform/Pages/P.razor", "@using CslaStarter.Data", "pages and components must not use Data")]
public void Check_ReportsTheViolation(string path, string line, string rule) =>
    Assert.Contains($"{path}: {rule}", Violation(path, line));

[Fact]
public void Check_AllowsHostCodeToUseData() =>
    Assert.Empty(Violation("src/CslaStarter.Web/Platform/Startup/A.cs", "using CslaStarter.Data;"));

[Fact]
public void Repository_HasNoBoundaryViolations() => Assert.Empty(Boundaries.Check(RepoPaths.Root));

[Fact]
public void Domain_ReferencesNoDataOrWebAssembly() => Assert.DoesNotContain(
    typeof(DomainAssembly).Assembly.GetReferencedAssemblies(),
    a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore") || a.Name.StartsWith("Npgsql") || a.Name.StartsWith("Microsoft.AspNetCore"));

[Fact]
public void Data_DoesNotReferenceCsla() => Assert.DoesNotContain(
    typeof(DataAssembly).Assembly.GetReferencedAssemblies(), a => a.Name!.StartsWith("Csla"));
```

- [ ] **Step 2: Run.** Expected: FAIL to compile, `Boundaries` does not exist.
- [ ] **Step 3: Implement `Boundaries.Check`.** A source scan of `.cs` and `.razor` files under `src/` (skipping `bin`, `obj`, `Migrations`). "Pages and components" means `src/CslaStarter.Web/Platform/Components/**`, `Platform/Pages/**` and `Features/**`. The feature rule compares the feature folder name of the file with the one in the `using`.
- [ ] **Step 4: Run.** Expected: 13 passed.
- [ ] **Step 5: Commit** `DEV-39 Enforce the project boundaries`.

### Task 3: Carried-over conventions, the design system move, and the plumbing

**Files:**
- Move: `src/CslaStarter.Components/wwwroot/gcds` → `src/CslaStarter.Web/wwwroot/gcds` (`git mv`), then delete `src/CslaStarter.Components`
- Port with their tests from `claude`: `RazorConventionTests.cs`, `GcdsAssetsTests.cs`, `ClaudeDocsTests.cs`, `WorkflowTests.cs`, `TemplateConventionTests.cs`, `TemplateRepositoryTests.cs`, `EnvironmentTests.cs`, `DevEnvironmentTests.cs` (drop any assertion about a removed project)
- Modify: `scripts/update-gcds.sh`, `.gitattributes`, `scripts/tests/template.test.sh`, `.github/workflows/ci.yml`, `.github/workflows/e2e.yml`, `.template.config/template.json`, `CLAUDE.md`, `README.md`, `README.app.md`
- Create: `CLAUDE.md` in each of the three `src` projects and in `Architecture.Tests` (each later test project brings its own when it is created); `docs/handbook/getting-started.md`, `docs/handbook/architecture.md`
- Delete: `docs/claude/*.md` except `workflow.md` and `template-dev.md`; `docs/claude/recipes/`; `.claude/skills/*`

**Interfaces:**
- Produces: the design system served from `/gcds/...` (it is now the host's own `wwwroot`, no `_content/` prefix).

- [ ] **Step 1: Port the tests first**, repointed at the new paths, and add one rule to `RazorConventionTests`:

```csharp
[Fact]
public void Markup_HasNoInlineStyleAttribute() => Assert.DoesNotContain(
    SourceFiles.Under("src", "*.razor"), f => Regex.IsMatch(File.ReadAllText(f), @"\sstyle\s*="));
```

  In `scripts/tests/template.test.sh`, the generated app must contain `src/Acme.Grants.Domain/CLAUDE.md` and `docs/handbook/architecture.md`, and the design-system diff compares `src/CslaStarter.Web/wwwroot/gcds` with `src/Acme.Grants.Web/wwwroot/gcds`.
- [ ] **Step 2: Run.** `dotnet test --project tests/CslaStarter.Architecture.Tests; bash scripts/tests/run.sh` — Expected: FAIL. `GcdsAssetsTests` cannot find `src/CslaStarter.Web/wwwroot/gcds`; `ClaudeDocsTests` reports project folders with no `CLAUDE.md` and broken guide links; the template tests fail on the missing files.
- [ ] **Step 3: Make them pass.**
  - Move the design system; update the path in `scripts/update-gcds.sh` and `.gitattributes`. Run `bash scripts/update-gcds.sh` and confirm `git status` shows no change under `wwwroot/gcds` (the fetch is identical).
  - Root `CLAUDE.md`: keep the Jira line, commands and workflow; replace the architecture map with the three projects and the `Platform`/`Features` rule; replace the golden rules with the Global Constraints above; point "Guides" at `docs/handbook/` and the spec. Each project `CLAUDE.md` is five to ten lines: what the project holds, what it must not reference, where its tests are.
  - `docs/handbook/architecture.md`: the structure and boundaries of spec §5, in plain words. `docs/handbook/getting-started.md`: the commands table.
  - `ci.yml`: remove the coverage step (Task 14 restores it for the new projects) and the end-to-end compile step (Task 13 restores it); keep restore, format, build, test and the script tests.
  - `e2e.yml`: leave only `workflow_dispatch` as its trigger until Task 13, which restores push and schedule and adds `pull_request: branches: [claude]` so the browser tests run on every pull request [§15].
  - `template.json` and the READMEs: no path may name a removed project.
- [ ] **Step 4: Run everything.** `dotnet build -c Release && dotnet test -c Release --no-build && dotnet format --verify-no-changes && bash scripts/tests/run.sh` — Expected: all green; script tests report 0 failed.
- [ ] **Step 5: Commit** `DEV-39 Carry over the conventions and repoint the plumbing`.

### Task 4: CI on the branch

- [ ] **Step 1: Ask Said** for the go-ahead to push `DEV-39-starter-rebuild` and open a **draft** pull request into `claude` titled `DEV-39 Starter rebuild (do not merge until complete)`. Do neither without it.
- [ ] **Step 2: Push and open the draft.** Bind it with the PR tools and read CI once. Expected: CI and CodeQL are green. E2E does not run yet (Task 13 switches it on).
- [ ] **Step 3: Slice 0 demonstration.** Report: the solution tree, the boundary test names, and the green CI run.

---

# Part B — Slice 1: the walking skeleton

### Task 5: Text in the domain: labels, messages and rule attributes

**Files:**
- Create: `src/CslaStarter.Data/Seed/translations.json`, `src/CslaStarter.Data/Seed/error-messages.json`
- Create in `src/CslaStarter.Domain/Platform/Texts/`: `Text.cs`, `ITranslator.cs`, `SourceTextTranslator.cs`, `ErrorMessageType.cs`, `ErrorMessageFallback.cs`, `LabelAttribute.cs`, `RequiredFieldAttribute.cs`, `MaxLengthOfAttribute.cs`
- Modify: `src/CslaStarter.Domain/CslaStarter.Domain.csproj` (embed `..\CslaStarter.Data\Seed\error-messages.json` as a linked resource)
- Test: `tests/CslaStarter.Domain.Tests/Texts/TextTests.cs`, `RuleAttributeTests.cs`; helper `tests/CslaStarter.Domain.Tests/Culture.cs` (`IDisposable Culture.Use(string name)`)

**Interfaces:**
- Produces:
  - `interface ITranslator { string Translate(string source, CultureInfo culture); string ErrorMessage(int code, CultureInfo culture); }`
  - `static class Text { static ITranslator Current { get; } static void Use(ITranslator translator); static string T(string source, params object?[] args); static string Error(int code, params object?[] args); }` Both read `CultureInfo.CurrentUICulture`. Arguments are applied with `string.Format` only when there are any.
  - `SourceTextTranslator` (the default): English is the source string, French is `"fr-" + source`, any other culture is English; error messages come from `ErrorMessageFallback`.
  - `static class ErrorMessageType { public const int Required = 1; public const int MaxLength = 2; }` (it becomes a `CslaType` in slice 3; the constants stay).
  - `LabelAttribute(string source) : DisplayNameAttribute`; `RequiredFieldAttribute : RequiredAttribute`; `MaxLengthOfAttribute(int maximum) : StringLengthAttribute`.
- Seed, exact text: code 1 `{0} is required.` / `Le champ {0} est obligatoire.`; code 2 `{0} must be {1} characters or fewer.` / `Le champ {0} ne doit pas dépasser {1} caractères.` JSON rows are `{ "code", "en", "fr" }` and `{ "source", "en", "fr" }`.
- Tests never call `Text.Use`: it is process-wide and tests run in parallel. They rely on the default translator.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public void T_English_IsTheSourceString() { using var _ = Culture.Use("en-CA"); Assert.Equal("Save", Text.T("Save")); }
[Fact] public void T_French_Unseeded_IsMarked() { using var _ = Culture.Use("fr-CA"); Assert.Equal("fr-Save", Text.T("Save")); }
[Fact] public void T_OtherCulture_IsEnglish() { using var _ = Culture.Use("es-MX"); Assert.Equal("Save", Text.T("Save")); }
[Fact] public void T_FillsPlaceholders() { using var _ = Culture.Use("en-CA"); Assert.Equal("Updated by Ada", Text.T("Updated by {0}", "Ada")); }
[Fact] public void T_WithoutArguments_LeavesPlaceholdersAlone() { using var _ = Culture.Use("en-CA"); Assert.Equal("Updated by {0}", Text.T("Updated by {0}")); }
[Fact] public void Error_UsesTheSeedText() { using var _ = Culture.Use("fr-CA"); Assert.Equal("Le champ Nom est obligatoire.", Text.Error(ErrorMessageType.Required, "Nom")); }
[Fact] public void Error_UnknownCode_NamesTheCode() { using var _ = Culture.Use("en-CA"); Assert.Contains("999", Text.Error(999)); }

[Fact] public void Label_FollowsTheCulture()
{
    var label = new LabelAttribute("Display name");
    using (Culture.Use("en-CA")) Assert.Equal("Display name", label.DisplayName);
    using (Culture.Use("fr-CA")) Assert.Equal("fr-Display name", label.DisplayName);
}
[Fact] public void RequiredField_Message_ComesFromErrorMessageType()
{ using var _ = Culture.Use("en-CA"); Assert.Equal("Display name is required.", new RequiredFieldAttribute().FormatErrorMessage("Display name")); }
[Fact] public void MaxLengthOf_Message_NamesTheLimit()
{ using var _ = Culture.Use("en-CA"); Assert.Equal("Display name must be 200 characters or fewer.", new MaxLengthOfAttribute(200).FormatErrorMessage("Display name")); }
```

- [ ] **Step 2: Run.** `dotnet test --project tests/CslaStarter.Domain.Tests` — Expected: FAIL to compile.
- [ ] **Step 3: Implement** the types above. `Text.Current` starts as a `SourceTextTranslator`. `translations.json` starts as an empty array; later tasks add rows.
- [ ] **Step 4: Run.** Expected: 10 passed.
- [ ] **Step 5: Commit** `DEV-39 Add database-ready labels, messages and rule attributes`.

### Task 6: Base classes and the first business objects

**Files:**
- Create in `src/CslaStarter.Domain/Platform/`: `AppRootBase.cs`, `AppReadOnlyBase.cs`, `AppReadOnlyListBase.cs`, `IUnitOfWork.cs`, `IStagedRow.cs`, `ICurrentUser.cs`, `DalExceptions.cs`, `Security/AppClaimTypes.cs`, `Security/CslaCurrentUser.cs`, `Security/IsSignedIn.cs`
- Create in `src/CslaStarter.Domain/Platform/Users/`: `UserEdit.cs`, `UserList.cs`, `UserInfo.cs`, `IUserDal.cs`, `UserDto.cs`
- Test: `tests/CslaStarter.Domain.Tests/Users/UserEditTests.cs`, `UserListTests.cs`, `FakeUserDal.cs`; helper `TestPortal.cs`

**Interfaces:**
- Consumes: Task 5's attributes and `Text`.
- Produces:
  - `interface IUnitOfWork { Task CommitAsync(CancellationToken cancellationToken = default); }`
  - `interface IStagedRow { uint Version { get; } }` (valid after the commit)
  - `interface ICurrentUser { bool IsSignedIn { get; } Guid? UserId { get; } string DisplayName { get; } }`; `CslaCurrentUser(ApplicationContext)` implements it from the claims `AppClaimTypes.UserId` and `AppClaimTypes.DisplayName`.
  - `sealed class NotFoundException(string type, object key) : Exception`; `sealed class ConcurrencyException(string type, object key) : Exception`
  - `abstract class AppRootBase<T> : BusinessBase<T>` with `public uint Version { get; }` and `protected void LoadVersion(uint version)`
  - `sealed record UserDto(Guid Id, string DisplayName, uint Version)`
  - `interface IUserDal { Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct); Task<UserDto> GetAsync(Guid id, CancellationToken ct); IStagedRow StageUpdate(UserDto user); }` `GetAsync` throws `NotFoundException`.
  - `UserEdit` (`[CslaImplementProperties]`, `[HistoryOf]` comes in slice 5): `Id` (private set); `DisplayName` with `[Label("Display name"), RequiredField, MaxLengthOf(200)]`; `[Fetch] Fetch(Guid id, [Inject] IUserDal dal)`; `[Update] Update([Inject] IUserDal dal, [Inject] IUnitOfWork work)`. No create, insert or delete: accounts come from sign-in.
  - `UserList : AppReadOnlyListBase<UserList, UserInfo>` with `[Fetch] Fetch([Inject] IUserDal dal)`; `UserInfo` has `Id` and `DisplayName` (`[Label("Display name")]`).
  - Object rules on all three: get and edit need `IsSignedIn` (ruling 8).
- `TestPortal` builds a service provider with `AddCsla()`, the fake DAL, a counting `IUnitOfWork` and a signed-in principal, and exposes `FetchAsync<T>(params object[] criteria)`, `Dal`, `Work`, and `SignedOut()`.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public async Task Fetch_LoadsTheUser()
{ var user = await _portal.FetchAsync<UserEdit>(Id); Assert.Equal("Ada", user.DisplayName); Assert.Equal(7u, user.Version); Assert.False(user.IsDirty); Assert.False(user.IsNew); }

[Theory, InlineData(""), InlineData("   ")]
public async Task DisplayName_Blank_BreaksTheRequiredRule(string value)
{
    using var _ = Culture.Use("en-CA");
    var user = await _portal.FetchAsync<UserEdit>(Id);
    user.DisplayName = value;
    Assert.False(user.IsValid);
    Assert.Equal("Display name is required.", user.BrokenRulesCollection.GetFirstBrokenRule(UserEdit.DisplayNameProperty)!.Description);
}

[Fact] public async Task DisplayName_Allows200_Rejects201()
{
    using var _ = Culture.Use("en-CA");
    var user = await _portal.FetchAsync<UserEdit>(Id);
    user.DisplayName = new string('a', 200); Assert.True(user.IsValid);
    user.DisplayName = new string('a', 201);
    Assert.Equal("Display name must be 200 characters or fewer.", user.BrokenRulesCollection.GetFirstBrokenRule(UserEdit.DisplayNameProperty)!.Description);
}

[Fact] public async Task Label_And_Message_FollowTheLanguage()
{
    using var _ = Culture.Use("fr-CA");
    Assert.Equal("fr-Display name", UserEdit.DisplayNameProperty.FriendlyName);
    var user = await _portal.FetchAsync<UserEdit>(Id);
    user.DisplayName = "";
    Assert.Equal("Le champ fr-Display name est obligatoire.", user.BrokenRulesCollection.GetFirstBrokenRule(UserEdit.DisplayNameProperty)!.Description);
}

[Fact] public async Task Save_StagesTheChange_CommitsOnce_And_TakesTheNewVersion()
{
    var user = await _portal.FetchAsync<UserEdit>(Id);
    user.DisplayName = "Ada L.";
    user = await user.SaveAsync();
    Assert.Equal(new UserDto(Id, "Ada L.", 7), _portal.Dal.Staged.Single());
    Assert.Equal(1, _portal.Work.Commits);
    Assert.Equal(8u, user.Version); Assert.False(user.IsDirty);
}

[Fact] public async Task Save_Invalid_Throws_And_StagesNothing()
{
    var user = await _portal.FetchAsync<UserEdit>(Id);
    user.DisplayName = "";
    await Assert.ThrowsAsync<Csla.Rules.ValidationException>(() => user.SaveAsync());
    Assert.Empty(_portal.Dal.Staged); Assert.Equal(0, _portal.Work.Commits);
}

[Fact] public async Task Fetch_SignedOut_IsRefused() =>
    await Assert.ThrowsAnyAsync<System.Security.SecurityException>(() => _portal.SignedOut().FetchAsync<UserEdit>(Id));

[Fact] public async Task List_ReturnsEveryUser_ReadOnly()
{ var list = await _portal.FetchAsync<UserList>(); Assert.Equal(["Ada", "Bea"], list.Select(u => u.DisplayName)); Assert.True(list.IsReadOnly); }
```

- [ ] **Step 2: Run.** Expected: FAIL to compile.
- [ ] **Step 3: Implement.** If the generator does not accept the base class, a nullable type or the attributes, stop and record it: the fallback is hand-written properties in the same order [§20], and that is a ruling Said must see.
- [ ] **Step 4: Run.** Expected: 9 passed, 0 warnings in the build (CSLA analyzers included).
- [ ] **Step 5: Commit** `DEV-39 Add the base classes and the Users business objects`.

### Task 7: The database: model, migration, DALs, sign-in directory, text seed

**Files:**
- Create in `src/CslaStarter.Data/Platform/`: `AppDbContext.cs`, `AppDbContextFactory.cs` (design time), `DataServices.cs`, `Users/AppUser.cs`, `Users/AppUserConfiguration.cs`, `Users/UserDal.cs`, `Users/UserDirectory.cs`, `Texts/TranslationRow.cs`, `Texts/ErrorMessageRow.cs`, `Texts/TextConfiguration.cs`, `Texts/TextDal.cs`, `Texts/TextSeeder.cs`
- Create in `src/CslaStarter.Domain/Platform/`: `Users/IUserDirectory.cs`, `Texts/ITextDal.cs`, `Texts/TextDtos.cs`
- Create: `src/CslaStarter.Data/Migrations/*_Initial.cs`
- Test: `tests/CslaStarter.Integration.Tests/PostgresFixture.cs` (port), `SchemaTests.cs`, `UserDirectoryTests.cs`, `TextStoreTests.cs`, `UserDalTests.cs`

**Interfaces:**
- Consumes: `IUserDal`, `UserDto`, `NotFoundException` (Task 6).
- Produces:
  - `sealed record SignedInUser(Guid Id, string DisplayName, bool IsActive)`; `interface IUserDirectory { Task<SignedInUser> EnsureAsync(string tenantId, string objectId, string displayName, CancellationToken ct); }`
  - `sealed record TranslationDto(int Code, string SourceString, string EnDesc, string FrDesc, bool IsActive)`; `sealed record ErrorMessageDto(int Code, string EnDesc, string FrDesc, bool IsActive)`
  - `interface ITextDal { Task<IReadOnlyList<TranslationDto>> GetTranslationsAsync(CancellationToken ct); Task<IReadOnlyList<ErrorMessageDto>> GetErrorMessagesAsync(CancellationToken ct); Task AddTranslationAsync(string sourceString, CancellationToken ct); }`
  - `static IServiceCollection AddAppData(this IServiceCollection services, string connectionString)`: registers `IDbContextFactory<AppDbContext>`, every `I*Dal` implementation found by scanning the `Data` assembly (scoped), `IUserDirectory`, `TextSeeder`.
  - `TextSeeder.SeedAsync(CancellationToken)`: inserts seed rows that are missing; never changes a row that exists.
  - Tables (snake_case): `app_user` (`id` uuid, `tenant_id`, `object_id`, `display_name`, `is_active`; unique on tenant and object id), `translation_type` (`code` identity, `source_string` unique, `en_desc`, `fr_desc`, `is_active`, `added_date`), `error_message_type` (`code` not generated, same text columns). `xmin` is the concurrency token on `app_user`.
- `UserDirectory`, `TextDal` and `TextSeeder` are platform stores: each call uses its own short-lived context from the factory [§7.2]. `UserDal` cannot be completed until Task 8 gives it the unit of work; here it implements the two reads on a factory context and `StageUpdate` throws `NotImplementedException`.
- New ids are `Guid.CreateVersion7()`.

- [ ] **Step 1: Write the failing tests** (each test class takes the `PostgresFixture`, which migrates a fresh database).

```csharp
[Fact] public async Task Schema_HasTheSlice1Tables_InSnakeCase() =>
    Assert.Superset(new HashSet<string> { "app_user", "translation_type", "error_message_type" }, await _db.TableNamesAsync());

[Fact] public async Task FirstSignIn_CreatesAnActiveUser_WithNoOtherRow()
{
    var user = await _directory.EnsureAsync("t1", "o1", "Ada", default);
    Assert.True(user.IsActive); Assert.Equal("Ada", user.DisplayName);
    Assert.Equal(user.Id, (await _directory.EnsureAsync("t1", "o1", "Renamed in Entra", default)).Id);
    Assert.Equal(1, await _db.CountAsync("app_user"));
}

[Fact] public async Task SecondSignIn_KeepsTheNameEditedInTheApp()
{
    var first = await _directory.EnsureAsync("t1", "o2", "Bea", default);
    await _db.ExecuteAsync("update app_user set display_name = 'Bea B.' where id = @id", first.Id);
    Assert.Equal("Bea B.", (await _directory.EnsureAsync("t1", "o2", "Bea", default)).DisplayName);
}

[Fact] public async Task ConcurrentFirstSignIns_CreateOneUser()
{
    var users = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _directory.EnsureAsync("t1", "o3", "Cy", default)));
    Assert.Single(users.Select(u => u.Id).Distinct());
}

[Fact] public async Task Seeder_InsertsMissingRows_And_NeverOverwrites()
{
    await _seeder.SeedAsync(default);
    await _db.ExecuteAsync("update error_message_type set fr_desc = 'Modifié' where code = 1");
    await _seeder.SeedAsync(default);
    var required = (await _text.GetErrorMessagesAsync(default)).Single(m => m.Code == ErrorMessageType.Required);
    Assert.Equal("{0} is required.", required.EnDesc); Assert.Equal("Modifié", required.FrDesc);
}

[Fact] public async Task AddTranslation_Twice_KeepsOneRow_MarkedInFrench()
{
    await _text.AddTranslationAsync("Brand new", default); await _text.AddTranslationAsync("Brand new", default);
    var row = (await _text.GetTranslationsAsync(default)).Single(t => t.SourceString == "Brand new");
    Assert.Equal(("Brand new", "fr-Brand new", true), (row.EnDesc, row.FrDesc, row.IsActive));
}

[Fact] public async Task Get_UnknownUser_ThrowsNotFound() =>
    await Assert.ThrowsAsync<NotFoundException>(() => _users.GetAsync(Guid.CreateVersion7(), default));
```

- [ ] **Step 2: Run.** Expected: FAIL to compile.
- [ ] **Step 3: Implement**, then create the migration: `dotnet ef migrations add Initial -p src/CslaStarter.Data -s src/CslaStarter.Data` (the design-time factory reads `CSLASTARTER_DESIGN_CONNECTION`; no database is needed to add a migration). `AddTranslationAsync` is `insert … on conflict (source_string) do nothing`.
- [ ] **Step 4: Run.** `dotnet test --project tests/CslaStarter.Integration.Tests` — Expected: 7 passed. `BoundaryTests` still pass.
- [ ] **Step 5: Commit** `DEV-39 Add the data model, sign-in directory and text store`.

### Task 8: The unit of work and audit capture

**Files:**
- Create in `src/CslaStarter.Data/Platform/`: `UnitOfWork.cs`, `Auditing/AuditedAttribute.cs`, `Auditing/AuditEventRow.cs`, `Auditing/AuditChangeRow.cs`, `Auditing/AuditConfiguration.cs`, `Auditing/AuditInterceptor.cs`
- Modify: `Users/UserDal.cs`, `Users/AppUser.cs` (`[Audited("User")]`), `DataServices.cs`, the `Initial` migration (regenerate)
- Test: `tests/CslaStarter.Integration.Tests/UnitOfWorkTests.cs`

**Interfaces:**
- Consumes: `IUnitOfWork`, `IStagedRow`, `ICurrentUser`, `ConcurrencyException` (Task 6).
- Produces:
  - `sealed class UnitOfWork : IUnitOfWork, IAsyncDisposable`, scoped. `internal Task<AppDbContext> DbAsync(CancellationToken)` opens the context and the transaction on first use and sets the transaction-local setting `app.user_id`. DALs take `UnitOfWork`, never a context. `UserDal` now uses it for its reads as well as `StageUpdate`, so one data portal operation is one transaction [§7.2].
  - `Data` grants `InternalsVisibleTo("CslaStarter.Integration.Tests")`. The test helper `scope.ScalarAsync<T>(sql)` runs on the unit of work's own connection and transaction.
  - `CommitAsync`: saves the staged changes and their audit rows, then commits. A stale root version becomes `ConcurrencyException`. A scope that ends without a commit rolls back.
  - Tables: `audit_event` (`id`, `occurred_on` from the database clock, `user_id`, `user_name`, `trace_id`, `action` Added/Updated/Deleted, `entity_type`, `entity_key`, `root_type`, `root_key`) and `audit_change` (`event_id`, `property`, `old_value`, `new_value`). Session, source address and row label are added by slices 2 and 5 [§7.5].
- Port the capture logic from `claude:src/CslaStarter.Data/Auditing/AuditSaveChangesInterceptor.cs`; `pg_notify` is slice 2.

- [ ] **Step 1: Write the failing tests.** `Scope(userId, name)` builds a DI scope over the fixture's database with that current user and returns its `UnitOfWork` and `IUserDal`.

```csharp
[Fact] public async Task Commit_WritesTheChange_And_ItsAudit_Together()
{
    await using var scope = Scope(Admin.Id, "Ada");
    var staged = scope.Users.StageUpdate(new UserDto(Bea.Id, "Bea B.", Bea.Version));
    await scope.Work.CommitAsync();

    Assert.Equal("Bea B.", await _db.ScalarAsync<string>("select display_name from app_user where id = @id", Bea.Id));
    Assert.NotEqual(Bea.Version, staged.Version);
    var audit = await _db.SingleAsync("select * from audit_event where entity_key = @k", Bea.Id.ToString());
    Assert.Equal(("Updated", "User", "User", Bea.Id.ToString(), Admin.Id, "Ada"),
        (audit.action, audit.entity_type, audit.root_type, audit.root_key, audit.user_id, audit.user_name));
    var change = await _db.SingleAsync("select * from audit_change where event_id = @e", audit.id);
    Assert.Equal(("DisplayName", "Bea", "Bea B."), (change.property, change.old_value, change.new_value));
}

[Fact] public async Task NoCommit_WritesNothing()
{
    await using (var scope = Scope(Admin.Id, "Ada")) { scope.Users.StageUpdate(new UserDto(Bea.Id, "Lost", Bea.Version)); }
    Assert.Equal("Bea", await _db.ScalarAsync<string>("select display_name from app_user where id = @id", Bea.Id));
    Assert.Equal(0, await _db.CountAsync("audit_event"));
}

[Fact] public async Task StaleVersion_ThrowsConcurrency_And_WritesNoAudit()
{
    await using var scope = Scope(Admin.Id, "Ada");
    scope.Users.StageUpdate(new UserDto(Bea.Id, "Too late", Bea.Version + 1));
    await Assert.ThrowsAsync<ConcurrencyException>(() => scope.Work.CommitAsync());
    Assert.Equal(0, await _db.CountAsync("audit_event"));
}

[Fact] public async Task UserId_IsVisibleInsideTheTransaction_AndNowhereElse()
{
    await using (var scope = Scope(Admin.Id, "Ada"))
        Assert.Equal(Admin.Id.ToString(), await scope.ScalarAsync<string>("select current_setting('app.user_id', true)"));
    Assert.True(string.IsNullOrEmpty(await _db.ScalarAsync<string>("select current_setting('app.user_id', true)")));
}

[Fact] public async Task UnchangedSave_WritesNoAudit()
{
    await using var scope = Scope(Admin.Id, "Ada");
    scope.Users.StageUpdate(new UserDto(Bea.Id, "Bea", Bea.Version));
    await scope.Work.CommitAsync();
    Assert.Equal(0, await _db.CountAsync("audit_change"));
}
```

- [ ] **Step 2: Run.** Expected: FAIL to compile.
- [ ] **Step 3: Implement**, regenerate `Initial`, and add to `Boundaries.Check`: a file under `src/CslaStarter.Data` outside `Platform/` that mentions `AppDbContext` is a violation ("DALs reach data through the unit of work"), with its `InlineData` row first.
- [ ] **Step 4: Run.** Expected: `UnitOfWorkTests` 5 passed; Task 7's tests still pass; `Data_DoesNotReferenceCsla` passes.
- [ ] **Step 5: Commit** `DEV-39 Add the unit of work with audit in the same transaction`.

### Task 9: The cached translator

**Files:**
- Create: `src/CslaStarter.Domain/Platform/Texts/CachedTranslator.cs`
- Test: `tests/CslaStarter.Domain.Tests/Texts/CachedTranslatorTests.cs` (fake `ITextDal`), `tests/CslaStarter.Integration.Tests/TranslatorTests.cs`

**Interfaces:**
- Consumes: `ITextDal`, DTOs (Task 7); `ITranslator`, `ErrorMessageFallback` (Task 5).
- Produces: `sealed class CachedTranslator(Func<ITextDal> dal) : ITranslator` with `Task LoadAsync(CancellationToken)` (also the reload), and `Task FlushAsync(CancellationToken)` which inserts every string seen since the last flush that had no row. `Translate` and `ErrorMessage` never touch the database and never throw.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public async Task Known_ReturnsTheRowForTheLanguage()
{ await _translator.LoadAsync(default); Assert.Equal("Enregistrer", _translator.Translate("Save", Fr)); Assert.Equal("Save", _translator.Translate("Save", En)); }

[Fact] public async Task OtherCulture_GetsEnglish()
{ await _translator.LoadAsync(default); Assert.Equal("Save", _translator.Translate("Save", new CultureInfo("es-MX"))); }

[Fact] public async Task Unknown_ReturnsTheMarkedFallback_And_IsInsertedOnce()
{
    await _translator.LoadAsync(default);
    for (var i = 0; i < 3; i++) Assert.Equal("fr-New label", _translator.Translate("New label", Fr));
    await _translator.FlushAsync(default);
    Assert.Equal(["New label"], _dal.Added);
}

[Fact] public void BeforeLoad_AnswersFromTheFallback()
{ Assert.Equal("Save", _translator.Translate("Save", En)); Assert.Equal("{0} is required.", _translator.ErrorMessage(ErrorMessageType.Required, En)); }

[Fact] public async Task LoadFailure_KeepsAnswering()
{ _dal.FailReads = true; await Assert.ThrowsAnyAsync<Exception>(() => _translator.LoadAsync(default)); Assert.Equal("fr-Save", _translator.Translate("Save", Fr)); }

[Fact] public async Task Reload_PicksUpAnEdit()
{ await _translator.LoadAsync(default); _dal.Edit("Save", fr: "Sauvegarder"); await _translator.LoadAsync(default); Assert.Equal("Sauvegarder", _translator.Translate("Save", Fr)); }

[Fact] public async Task ErrorMessage_ComesFromTheRow_ThenTheFallback()
{ await _translator.LoadAsync(default); Assert.Equal("Edited: {0}", _translator.ErrorMessage(ErrorMessageType.Required, En)); Assert.Equal("{0} must be {1} characters or fewer.", _translator.ErrorMessage(ErrorMessageType.MaxLength, En)); }

// Integration, real PostgreSQL: two servers meet the same new string.
[Fact] public async Task TwoTranslators_SameMissingString_LeaveOneRow()
{
    var (a, b) = (NewTranslator(), NewTranslator());
    a.Translate("Shared new", Fr); b.Translate("Shared new", Fr);
    await Task.WhenAll(a.FlushAsync(default), b.FlushAsync(default));
    Assert.Equal(1, await _db.CountAsync("translation_type", "source_string = 'Shared new'"));
}
```

- [ ] **Step 2: Run.** Expected: FAIL to compile.
- [ ] **Step 3: Implement.** The cache is an immutable dictionary swapped on load. Strings waiting for insertion are a concurrent set.
- [ ] **Step 4: Run.** Expected: 7 passed in `Domain.Tests`, 1 in `Integration.Tests`.
- [ ] **Step 5: Commit** `DEV-39 Add the cached translator with run-time additions`.

### Task 10: The host: CSLA, sign-in, headers, culture, text startup

**Files:**
- Create in `src/CslaStarter.Web/Platform/`: `Startup/CslaSetup.cs`, `Startup/TextStartup.cs` (hosted service), `Identity/AuthenticationSetup.cs`, `Identity/SignInUser.cs`, `Identity/SignInEvents.cs`, `Security/SecurityHeaders.cs`, `Security/CspNonce.cs`, `Localization/LocalizationSetup.cs`
- Modify: `src/CslaStarter.Web/Program.cs`, `appsettings.json`, `appsettings.Development.json`
- Test: `tests/CslaStarter.Integration.Tests/WebAppFactory.cs`, `TestAuthHandler.cs` (port both), `HostTests.cs`, `SignInUserTests.cs`

**Interfaces:**
- Consumes: `AddAppData`, `TextSeeder`, `CachedTranslator`, `IUserDirectory`, `CslaCurrentUser`.
- Produces:
  - `Program` composes: `AddAppData`, `AddCsla` exactly as spec §6.1 minus the three hooks and the dashboard (slice 3 adds them), `ICurrentUser` → `CslaCurrentUser`, authentication, security headers, request localization, Razor components with interactive server mode.
  - `Authentication:Mode` is `MockOidc` or `Entra`. `MockOidc` outside Development and Testing stops startup with an error that names the setting. `Entra` is wired in slice 2 and for now also stops startup with "not available yet".
  - `static SignInUser SignInUser.From(ClaimsPrincipal principal)` → `(TenantId, ObjectId, DisplayName)`. The name is the `name` claim, else `preferred_username`, else the object id.
  - On sign-in, `SignInEvents` calls `IUserDirectory.EnsureAsync`, refuses an inactive user, and adds the claims `AppClaimTypes.UserId` and `AppClaimTypes.DisplayName`.
  - `GET /culture/set?culture=fr-CA&returnUrl=/x` sets the culture cookie and redirects. Only `en-CA` and `fr-CA` are accepted, and only a local `returnUrl`.
  - `CachedTranslator` is registered as a singleton, and as `ITranslator`.
  - The test factory runs in the `Testing` environment with `Authentication:Mode=MockOidc` and `Authentication:MockOidc:Authority=http://localhost:1/entra`; `SignedInClient()` and `ScopeSignedInAs(user)` use the ported `TestAuthHandler`.
  - `TextStartup`: on start, `TextSeeder.SeedAsync`, `CachedTranslator.LoadAsync`, `Text.Use(translator)`; then flushes missing strings every few seconds and once on stop. A failure to reach the database is logged and does not stop the host.
  - Headers on page responses: the CSP with a per-response nonce and `form-action 'self'` plus the sign-in provider's origin, HSTS, `nosniff`, referrer policy, permissions policy, opener policy, and `Cache-Control: no-store` [§8.3]. Port from `claude:src/CslaStarter.Infrastructure/Web/SecurityHeaders.cs` and `AuthenticationSetup.ProviderOrigin`.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public async Task Anonymous_IsSentToSignIn()
{ var response = await _factory.AnonymousClient().GetAsync("/"); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); }

[Fact] public async Task Page_CarriesTheSecurityHeaders()
{
    var response = await _factory.SignedInClient().GetAsync("/");
    var csp = response.Headers.GetValues("Content-Security-Policy").Single();
    Assert.Matches(@"script-src [^;]*'nonce-[A-Za-z0-9+/=]+'", csp);
    Assert.Contains("form-action 'self' http://localhost:1", csp);
    Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
}

[Fact] public void MockSignIn_InProduction_StopsStartup() =>
    Assert.Contains("Authentication:Mode", Assert.ThrowsAny<Exception>(() => _factory.StartIn("Production")).ToString());

[Theory]
[InlineData("fr-CA", "/admin/users", "/admin/users")]
[InlineData("fr-CA", "https://evil.example/", "/")]
[InlineData("de-DE", "/", null)]   // null: refused with 400
public async Task CultureEndpoint_AcceptsOnlyOurLanguages_And_LocalAddresses(string culture, string returnUrl, string? location) { /* asserts status, Location and the cookie */ }

[Fact] public async Task Startup_SeedsText_And_SwitchesToDatabaseText()
{
    await _factory.Services.GetRequiredService<CachedTranslator>().LoadAsync(default);
    using var _ = Culture.Use("fr-CA");
    Assert.Equal("Nom d'affichage", Text.T("Display name"));
}

[Fact] public async Task DataPortal_ThroughTheRealHost_ListsUsers()
{
    await using var scope = _factory.ScopeSignedInAs(Ada);
    var list = await scope.ServiceProvider.GetRequiredService<IDataPortal<UserList>>().FetchAsync();
    Assert.Contains(list, u => u.DisplayName == "Ada");
}

[Theory]
[InlineData("Ada L", "ada@x", "o1", "Ada L")]
[InlineData(null, "ada@x", "o1", "ada@x")]
[InlineData(null, null, "o1", "o1")]
public void SignInUser_PicksTheBestName(string? name, string? username, string oid, string expected) =>
    Assert.Equal(expected, SignInUser.From(Principal(name, username, oid)).DisplayName);
```

- [ ] **Step 2: Run.** Expected: FAIL to compile.
- [ ] **Step 3: Implement.** Add the seed row `Display name` / `Nom d'affichage` to `translations.json`.
- [ ] **Step 4: Run.** Expected: `HostTests` and `SignInUserTests` pass (11 cases).
- [ ] **Step 5: Commit** `DEV-39 Add the host: sign-in, headers, culture and text startup`.

### Task 11: The kit's core

**Files:**
- Create in `src/CslaStarter.Web/Platform/Components/`: `Base/CslaBaseComponent.cs`, `Forms/AppViewModel.cs`, `Forms/CslaForm.razor(.cs)`, `Fields/CslaFieldBase.cs`, `Fields/CslaTextField.razor(.cs)`, `Buttons/CslaButton.razor(.cs)`, `Notifications/CslaErrorSummary.razor(.cs)`, `Notifications/CslaLiveRegion.razor(.cs)`, `Interop/EventHandlers.cs`, `_Imports.razor`
- Create: `src/CslaStarter.Web/wwwroot/CslaStarter.Web.lib.module.js` (the design system's custom events)
- Test: `tests/CslaStarter.Web.Tests/ComponentTestContext.cs` (port), `Fields/TextFieldTests.cs`, `Forms/FormTests.cs`, `Buttons/ButtonTests.cs`

**Interfaces:**
- Consumes: `Text.T`, CSLA's `ViewModel<T>` and `Csla.Blazor.IPropertyInfo`.
- Produces:
  - `abstract class CslaBaseComponent : ComponentBase, IAsyncDisposable` with `protected string Id`, `protected CancellationToken Disposed`, `protected string T(string source, params object?[] args)`, `protected Task RunAsync(Func<Task> work)` (sets `IsBusy`, catches, reports through the error summary, never rethrows to the renderer), `protected bool IsBusy`.
  - `sealed class AppViewModel<T> : ViewModel<T>`; `ManageObjectLifetime` is off [§11.2].
  - `CslaForm<T>` parameters: `Func<Task<T>> Load`, `RenderFragment<T> ChildContent`, `EventCallback<T> Saved`. It cascades itself; exposes `T? Model`, `Task SaveAsync()`, `IReadOnlyList<string> Errors`. A failed save is read from the view model and shown; it is never swallowed.
  - `CslaTextField` parameter: `Expression<Func<string>> For`. It renders one `gcds-input` with `id`, `label`, `value`, `required`, `error-message`, `disabled`, `validate-on="other"`, all read from the property's `IPropertyInfo` and its rule attributes.
  - `CslaButton` parameters: `ButtonKind Kind` (`Save` only in this slice), `RenderFragment ChildContent`. Disabled when the form is busy or the model is not savable.
- Port the proven interop from `claude:src/CslaStarter.Components/` (`Interop/EventHandlers.cs`, `wwwroot/CslaStarter.Components.lib.module.js`, `Fields/CslaFieldBase.cs`, `Fields/CslaTextField.razor*`). Leave out drafts, presence, merge, shortcuts and undo levels. The event name and arguments type in the tests below (`ongcdschange`, `GcdsChangeEventArgs`) stand for whatever the ported `EventHandlers.cs` defines; use its names.

- [ ] **Step 1: Write the failing tests** (`Render` hosts the component in a `CslaForm<UserEdit>` loaded from a fake portal).

```csharp
[Fact] public void TextField_TakesLabelAndHintsFromTheProperty()
{
    var input = Render(user: Ada).Find("gcds-input");
    Assert.Equal("Display name", input.GetAttribute("label"));
    Assert.Equal("Ada", input.GetAttribute("value"));
    Assert.True(input.HasAttribute("required"));
    Assert.Equal("other", input.GetAttribute("validate-on"));
    Assert.False(string.IsNullOrEmpty(input.GetAttribute("id")));
}

[Fact] public void TextField_Change_WritesTheProperty_And_ShowsTheBrokenRule()
{
    var cut = Render(user: Ada);
    cut.Find("gcds-input").TriggerEvent("ongcdschange", new GcdsChangeEventArgs { Value = "" });
    Assert.Equal("", cut.Instance.Model!.DisplayName);
    Assert.Equal("Display name is required.", cut.Find("gcds-input").GetAttribute("error-message"));
}

[Fact] public async Task Form_SaveValid_Saves_RaisesSaved_And_Announces()
{
    var cut = Render(user: Ada); Change(cut, "Ada L.");
    await cut.InvokeAsync(() => cut.Instance.SaveAsync());
    Assert.Equal("Ada L.", _portal.Saved.Single().DisplayName);
    Assert.Equal(1, _savedCalls); Assert.Contains("Saved", cut.Find("[aria-live]").TextContent);
}

[Fact] public async Task Form_SaveInvalid_ShowsTheSummary_And_DoesNotSave()
{
    var cut = Render(user: Ada); Change(cut, "");
    await cut.InvokeAsync(() => cut.Instance.SaveAsync());
    Assert.Empty(_portal.Saved);
    Assert.Contains("Display name is required.", cut.Find("gcds-error-summary, [data-error-summary]").OuterHtml);
}

[Fact] public async Task Form_SaveFailure_IsShown_NotSwallowed()
{
    _portal.FailSaveWith(new ConcurrencyException("User", Ada.Id));
    var cut = Render(user: Ada); Change(cut, "Ada L.");
    await cut.InvokeAsync(() => cut.Instance.SaveAsync());
    Assert.NotEmpty(cut.Instance.Errors);
}

[Fact] public void Form_HasNoBrowserValidation() => Assert.True(Render(user: Ada).Find("form").HasAttribute("novalidate"));

[Fact] public void SaveButton_IsDisabled_UntilTheModelIsSavable()
{
    var cut = Render(user: Ada);
    Assert.True(cut.Find("gcds-button").HasAttribute("disabled"));
    Change(cut, "Ada L.");
    Assert.False(cut.Find("gcds-button").HasAttribute("disabled"));
}
```

- [ ] **Step 2: Run.** `dotnet test --project tests/CslaStarter.Web.Tests` — Expected: FAIL to compile.
- [ ] **Step 3: Implement.** Add the seed rows this task's text needs (`Save` / `Enregistrer`, `Saved` / `Enregistré`).
- [ ] **Step 4: Run.** Expected: 7 passed; `RazorConventionTests` and `BoundaryTests` pass.
- [ ] **Step 5: Commit** `DEV-39 Add the core of the component kit`.

### Task 12: The shell and the two pages

**Files:**
- Create in `src/CslaStarter.Web/Platform/Components/Layout/`: `App.razor`, `Routes.razor(.cs)`, `MainLayout.razor(.cs)`, `LanguageToggle.razor(.cs)`
- Create in `src/CslaStarter.Web/Platform/Pages/`: `Home.razor(.cs)`, `NotFound.razor(.cs)`, `Admin/UsersPage.razor(.cs)` (`/admin/users`), `Admin/UserEditPage.razor(.cs)` (`/admin/users/{Id:guid}`)
- Create: `src/CslaStarter.Web/wwwroot/app.css` (design-system tokens only)
- Test: `tests/CslaStarter.Web.Tests/Pages/PageTests.cs`, `Layout/LayoutTests.cs`

**Interfaces:**
- Consumes: the kit (Task 11), `IDataPortal<UserList>`, `IDataPortal<UserEdit>`, `/culture/set` (Task 10), the design system at `/gcds/...` (Task 3).
- Produces: the page addresses above. `App.razor` follows `claude:src/CslaStarter.Web/Components/App.razor` with the new asset paths, `<html lang>` from the culture, the `csp-nonce` meta tag, and `InteractiveServerRenderMode(prerender: false)`.
- The layout has the Government of Canada header with the language toggle and skip link, a `main` landmark holding the page's `h1`, and the footer. Navigation by page attribute arrives in slice 3; here the home page links to Users.
- `UserEditPage` shows the not-found content when the fetch throws `NotFoundException`.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public void Users_ListsEachUser_WithALinkToEdit()
{
    var cut = RenderPage<UsersPage>(users: [Ada, Bea]);
    Assert.Equal("Users", cut.Find("h1").TextContent.Trim());
    Assert.Equal([$"/admin/users/{Ada.Id}", $"/admin/users/{Bea.Id}"], cut.FindAll("table a").Select(a => a.GetAttribute("href")));
    Assert.Equal("Display name", cut.Find("table th").TextContent.Trim());
}

[Fact] public void EditUser_ShowsTheForm_ForThatUser()
{
    var cut = RenderPage<UserEditPage>(p => p.Add(x => x.Id, Ada.Id), users: [Ada]);
    Assert.Equal("Ada", cut.Find("gcds-input").GetAttribute("value"));
}

[Fact] public void EditUser_UnknownId_ShowsNotFound()
{
    var cut = RenderPage<UserEditPage>(p => p.Add(x => x.Id, Guid.CreateVersion7()), users: [Ada]);
    Assert.Empty(cut.FindAll("gcds-input")); Assert.Contains("Page not found", cut.Find("h1").TextContent);
}

[Fact] public void Home_LinksToUsers() => Assert.NotNull(RenderPage<Home>().Find("a[href='/admin/users']"));

[Fact] public void Layout_HasHeader_MainLandmark_And_LanguageToggle()
{
    var cut = RenderLayout();
    Assert.NotNull(cut.Find("gcds-header")); Assert.NotNull(cut.Find("main#main-content"));
    Assert.EndsWith("culture=fr-CA&returnUrl=%2F", cut.Find("gcds-lang-toggle, [data-lang-toggle]").GetAttribute("href"));
}
```

- [ ] **Step 2: Run.** Expected: FAIL to compile.
- [ ] **Step 3: Implement**, adding each new string to `translations.json` with real French (`Users` / `Utilisateurs`, `Home` / `Accueil`, `Edit user` / `Modifier l'utilisateur`, `Page not found` / `Page introuvable`, and any other this task introduces).
- [ ] **Step 4: Run.** Expected: 5 passed. The first look at the real pages is Task 13's browser run, which follows directly.
- [ ] **Step 5: Commit** `DEV-39 Add the layout shell, home, Users list and edit pages`.

### Task 13: Proven in a real browser

**Files:**
- Create: `tests/CslaStarter.E2E.Tests/CslaStarter.E2E.Tests.csproj`, `E2EEnvironment.cs` (port from `claude`, one app instance), `BrowserTests.cs`, `CLAUDE.md`
- Modify: `.github/workflows/ci.yml`, `.github/workflows/e2e.yml`

**Interfaces:**
- Consumes: the whole app. The fixture starts PostgreSQL and the mock sign-in server in containers, migrates, starts the app on Kestrel over HTTPS in the `Testing` environment with `Authentication:Mode=MockOidc`, and launches Chromium. Nothing starts unless `E2E=1`.
- Produces: `Task<IPage> SignInAsync(string subject, string path = "/", string culture = "en-CA")` and `Task<T> DbAsync<T>(string sql, object? args = null)`.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public async Task SignIn_LandsOnHome_And_CreatesTheAccount()
{
    E2E.Require();
    var page = await _env.SignInAsync("ada");
    await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Home");
    Assert.Equal(1, await _env.DbAsync<long>("select count(*) from app_user where object_id = 'ada'"));
}

[Fact] public async Task EditUser_ValidatesThenSaves_And_IsAudited()
{
    E2E.Require();
    var page = await _env.SignInAsync("bea", "/admin/users");
    await page.GetByRole(AriaRole.Link, new() { Name = "bea" }).ClickAsync();
    var name = page.GetByLabel("Display name");
    await name.FillAsync(""); await name.BlurAsync();
    await Assertions.Expect(page.GetByText("Display name is required.").First).ToBeVisibleAsync();
    await name.FillAsync("Bea B."); await name.BlurAsync();
    await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    await Assertions.Expect(page.GetByText("Saved")).ToBeVisibleAsync();
    Assert.Equal("Bea B.", await _env.DbAsync<string>("select display_name from app_user where object_id = 'bea'"));
    Assert.Equal(("bea", "Bea B."), await _env.DbAsync<(string, string)>(
        "select c.old_value, c.new_value from audit_change c join audit_event e on e.id = c.event_id where e.entity_type = 'User' and c.property = 'DisplayName'"));
}

[Fact] public async Task French_ComesFromTheDatabase_WithNoPlaceholder()
{
    E2E.Require();
    var page = await _env.SignInAsync("cy", "/admin/users", culture: "fr-CA");
    await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Utilisateurs");
    await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "fr-CA");
    Assert.DoesNotContain("fr-", await page.Locator("main").InnerTextAsync());
}

[Fact] public async Task LanguageToggle_SwitchesThePage()
{
    E2E.Require();
    var page = await _env.SignInAsync("dee", "/admin/users");
    await page.GetByRole(AriaRole.Link, new() { Name = "Français" }).ClickAsync();
    await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Utilisateurs");
}

[Theory, InlineData("0198f3a0-0000-7000-8000-000000000001"), InlineData("not-an-id")]
public async Task UnknownOrMalformedUser_ShowsNotFound_NotAnError(string id)
{
    E2E.Require();
    var page = await _env.SignInAsync("eve", $"/admin/users/{id}");
    await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Page not found");
}

[Theory]
[InlineData("/", "en-CA"), InlineData("/", "fr-CA")]
[InlineData("/admin/users", "en-CA"), InlineData("/admin/users", "fr-CA")]
[InlineData("/admin/users/me", "en-CA"), InlineData("/admin/users/me", "fr-CA")]   // "me" is replaced by the signed-in user's id
public async Task Page_PassesAxe(string path, string culture)
{
    E2E.Require();
    var page = await _env.SignInAsync("axe", path, culture);
    Assert.Empty((await page.RunAxe(new() { RunOnly = new() { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] } })).Violations);
}

[Fact] public async Task Browser_ReportsNoConsoleError_And_NoCspViolation() { /* collect console errors across home, list, edit, save; Assert.Empty */ }
```

- [ ] **Step 2: Run.** `dotnet build tests/CslaStarter.E2E.Tests` then, after Said agrees to the Chromium download into `/tmp` if it is not there, `E2E=1 dotnet test --project tests/CslaStarter.E2E.Tests --no-build` — Expected: FAIL at first for the missing fixture, then for real defects. Treat each real defect with superpowers:systematic-debugging: trace it in the browser, write the lower-level test that reproduces it, then fix.
- [ ] **Step 3: Fix until green.** Any string still showing `fr-` needs its seed row.
- [ ] **Step 4: Switch the workflows back on.** Restore the end-to-end compile step in `ci.yml` and the triggers in `e2e.yml`, with `pull_request: branches: [claude]` added (Task 3 switched them off).
- [ ] **Step 4b: Run three times in a row.** Expected: 13 passed each time, no flake.
- [ ] **Step 5: Commit** `DEV-39 Prove the walking skeleton in a real browser`.

### Task 14: Text completeness, the coverage gate, and the docs

**Files:**
- Create: `tests/CslaStarter.Architecture.Tests/TextTests.cs`, `BusinessLayoutTests.cs`
- Modify: `scripts/check-coverage.sh`, `scripts/tests/coverage.test.sh`, `.github/workflows/ci.yml`, `docs/handbook/architecture.md`, `docs/handbook/getting-started.md`, the project `CLAUDE.md` files

**Interfaces:**
- Produces: `scripts/check-coverage.sh <minimum> <assembly>[:<path fragment>] <report> …`. With a path fragment, only classes whose file path contains it count.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public void EveryStringUsedInCode_IsSeeded_WithRealFrench()
{
    var seed = Seed.Translations();   // source -> (en, fr)
    var missing = TextUsages.InSource().Where(s => !seed.TryGetValue(s, out var t) || t.Fr.StartsWith("fr-") || string.IsNullOrWhiteSpace(t.Fr));
    Assert.Empty(missing);
}
[Fact] public void T_And_Label_TakeOnlyLiterals() => Assert.Empty(TextUsages.NonLiteralCalls());
[Fact] public void ErrorMessageType_Constants_MatchTheSeedRows() =>
    Assert.Equal(Seed.ErrorMessages().Keys.Order(), Constants.Of(typeof(ErrorMessageType)).Order());
[Fact] public void ErrorMessages_HaveBothLanguages_And_TheSamePlaceholders() =>
    Assert.All(Seed.ErrorMessages().Values, m => Assert.Equal(Placeholders(m.En), Placeholders(m.Fr)));
[Fact] public void NoResxFiles() => Assert.Empty(SourceFiles.Under("src", "*.resx"));
[Fact] public void BusinessClasses_AreSealedPartial_InPropertiesRulesOperationsOrder() => Assert.Empty(BusinessLayout.Violations());
```

  In `scripts/tests/coverage.test.sh`: `test_coverage_counts_only_the_path_prefix` builds a Cobertura file with one class under `Platform/Components` at 90% and one elsewhere at 10%, and expects `check-coverage.sh 80 CslaStarter.Web:Platform/Components <file>` to pass and `check-coverage.sh 80 CslaStarter.Web <file>` to fail.
- [ ] **Step 2: Run.** Expected: FAIL to compile; the script test fails on the unknown argument form.
- [ ] **Step 3: Implement.** `TextUsages` reads `.cs` files with Roslyn (`Microsoft.CodeAnalysis.CSharp` is already pinned) for `T(...)`, `Text.T(...)` and `[Label(...)]`, and `.razor` files with a pattern for `T("…")`. `BusinessLayout` uses Roslyn on `src/CslaStarter.Domain`. Restore the CI coverage step for `CslaStarter.Domain` and `CslaStarter.Web:Platform/Components` at 80%.
- [ ] **Step 4: Run the whole suite.** `dotnet build -c Release && dotnet test -c Release --no-build && dotnet format --verify-no-changes && bash scripts/tests/run.sh`, the two coverage runs, and the browser tests once more — Expected: everything green; both coverage figures at or above 80%. If coverage is short, add the missing tests; do not lower the gate.
- [ ] **Step 5: Update the handbook** with what now exists: how a label and a message reach the screen, the unit of work, how to run each test project.
- [ ] **Step 6: Commit** `DEV-39 Gate text completeness and coverage`, push, and read CI once (CI and E2E on the draft pull request). Expected: both green.
- [ ] **Step 7: Slice 1 demonstration.** Report with a screenshot of the edit page in each language, the audit row, the axe result, and the test counts per project.

---

### Task 15: Claude guidance that a new session can rely on (spec §16.1)

**Files:**
- Modify: `CLAUDE.md`; the `CLAUDE.md` of all three `src` projects and all five test projects; `tests/CslaStarter.Architecture.Tests/ClaudeDocsTests.cs`; `scripts/check-coverage.sh`, `scripts/tests/coverage.test.sh`, `.github/workflows/ci.yml`; `.template.config/template.json` and `scripts/tests/template.test.sh` if a new path needs shipping or excluding
- Create: `docs/claude/working-agreement.md`, `docs/claude/business-rules.md`, `docs/claude/jobs.md`
- Create: `docs/handbook/recipes/add-a-field.md`, `add-a-translation.md`, `add-an-error-message.md`, `add-a-rule.md`
- Create: `.claude/skills/docs-check/SKILL.md`
- Create: `tests/CslaStarter.Architecture.Tests/RuleConventionTests.cs`, `DocFacts.cs`

**Interfaces:**
- Consumes: everything slice 1 built. Rule classes and rule attributes already live in `src/CslaStarter.Domain/Platform/Rules/` (moved there in Task 6).
- Produces:
  - Required sections in every project `CLAUDE.md`, in this order: `## What lives here`, `## Patterns to copy`, `## Never`, `## Tests`, `## Pitfalls`. The root has `## Start here` (what to read first, the commands that prove the tree is healthy) and `## Which file for which job` (a table: job → file).
  - `DocFacts.Extract(string markdown)`: the repository paths, `dotnet`/`bash` commands, project names and backticked type names a file states. A type name counts when it is backticked and PascalCase and the file marks it with a trailing `(type)` or sits in a `Patterns to copy` section.
  - `check-coverage.sh --branches …` also requires the branch rate to meet the minimum.
- The business rules guide holds the question checklist of spec §16.1 word for word, the outcome-table format, and one worked example taken from a real rule in the repository.
- Recipes describe only what exists at this commit (a field on an existing object; a translation; an error message; a rule attribute and a rule class). Later slices add the recipes for a permission, a type, a feature and a page with the code that makes them true.

- [ ] **Step 1: Bring the open questions to Said in one message** before writing the rules guide: anything about how rules should be asked about, worded or tested that the spec does not settle. Record the answers in the guide. If there are none, say so in the report.
- [ ] **Step 2: Write the failing tests.**

```csharp
[Fact] public void EveryProject_HasAClaudeFile_WithTheRequiredSections() =>
    Assert.Empty(ClaudeFiles.Projects().SelectMany(f => ClaudeFiles.MissingSections(f, "What lives here", "Patterns to copy", "Never", "Tests", "Pitfalls")));
[Fact] public void Root_HasStartHere_And_TheJobTable() =>
    Assert.Empty(ClaudeFiles.MissingSections(ClaudeFiles.Root, "Start here", "Which file for which job"));
[Fact] public void RootIndex_ReachesEveryProjectFile_And_EveryGuide() =>
    Assert.Empty(ClaudeFiles.All().Except(ClaudeFiles.ReachableFrom(ClaudeFiles.Root)));
[Fact] public void EveryPathNamed_Exists() => Assert.Empty(DocFacts.InDocs().Paths.Where(p => !RepoPaths.Exists(p)));
[Fact] public void EveryCommandNamed_PointsAtARealProjectOrScript() => Assert.Empty(DocFacts.InDocs().Commands.Where(c => !c.TargetExists));
[Fact] public void EveryTypeNamed_ExistsInSource() => Assert.Empty(DocFacts.InDocs().Types.Where(t => !SourceTypes.Exists(t)));
[Fact] public void DocFacts_FindsAPath_ACommand_AndAType_InASample() { /* a fixed markdown sample in, the three facts out */ }
[Fact] public void DocFacts_ReportsAMissingPath_InASample() { /* the checker itself is seen to fail */ }

[Fact] public void EveryRule_LivesInARulesFolder() => Assert.Empty(RuleTypes.InDomain().Where(t => !t.Namespace!.EndsWith(".Rules")));
[Fact] public void EveryRule_HasATestClass() => Assert.Empty(RuleTypes.InDomain().Where(t => !DomainTests.HasClass(t.Name + "Tests")));
[Fact] public void EveryRuleTestFile_StartsWithItsOutcomeTable() => Assert.Empty(RuleTypes.InDomain().Where(t => !DomainTests.HasOutcomeTable(t.Name + "Tests")));
```

  `RuleTypes.InDomain()` is every type in the `Domain` assembly that derives from a CSLA rule base, implements a CSLA rule interface, or derives from `ValidationAttribute`. In `scripts/tests/coverage.test.sh`: `test_coverage_branches_flag_checks_the_branch_rate` (a class at 100% lines and 50% branches fails with `--branches`, passes without).
- [ ] **Step 3: Run.** Expected: FAIL. Sections missing, guides unreachable, no outcome tables, unknown `--branches`.
- [ ] **Step 4: Write the files and close the gaps.** Add the outcome table and any missing outcome test to each existing rule's test file until `check-coverage.sh --branches 100 CslaStarter.Domain:/Rules/ <report>` passes; add that gate to `ci.yml`. Write the `docs-check` skill: it takes the current diff, finds the Claude files and handbook pages that cover each changed path, and reports every statement the change made false or left missing.
- [ ] **Step 5: Prove it with a fresh reader.** Dispatch one agent that has only the repository (no plan, no spec, no conversation) with the job "add a required `Title` field, 100 characters at most, to `UserEdit`, with its French label, and prove it works". It must finish from the Claude files alone. Its questions and wrong turns are defects in the files: fix them, discard its code, and repeat once.
- [ ] **Step 6: Run the whole suite and the script tests.** Expected: all green.
- [ ] **Step 7: Commit** `DEV-39 Add the Claude guidance system and its checks`, push, read CI once.

## Carried into slice 2 (not done here, on purpose)

- Users get-and-edit permission becomes Admin only; roles, `HasRole`, `bootstrap-admin` (ruling 8).
- Entra ID mode; server-side sessions; rate limits; startup checks and `doctor`; security events.
- The two database roles and their grants; the app still connects with one role here.
- `pg_notify` in the commit, the listener and the hub; the translator reloading on a change notice.
- Session and source address on audit rows.
