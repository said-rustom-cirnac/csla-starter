# Plan 6: Hardening and Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the repository into what spec §2 promises: a `dotnet new` template (with `--claude`) that produces a running, tested and secured app. It ships with:
- end-to-end tests against the mock Entra ID, with an axe accessibility scan;
- the remaining convention tests;
- a release workflow (template package, SBOM, EF migration bundle);
- Claude skills;
- complete guides.

**Architecture:**
- **Template:** `.template.config/template.json` at the repository root (`sourceName: CslaStarter`, short name `csla-blazor`). A `CslaStarter.Templates.csproj` (PackageType `Template`) packs the repository content from the `claude` branch.
- **E2E:** `tests/CslaStarter.E2E.Tests` uses Playwright, the app on Kestrel (`UseKestrel()` + `StartServer()` on a `WebApplicationFactory`), and mock-oauth2-server and PostgreSQL in Testcontainers.
  - It runs only where browsers and the mock image are available (`E2E=1`), in CI on merge to `claude` and nightly.

**Tech Stack:** `dotnet new` template engine, Microsoft.Playwright 1.64.1 (+ Microsoft.Playwright.Xunit.v3), Deque axe-core (vendored script), mock-oauth2-server 6.1.5, CycloneDX for the SBOM, `dotnet ef migrations bundle`.

**Spec:** `docs/superpowers/specs/2026-11-12-csla-starter-design.md`:
- §16 (end-to-end and architecture rows);
- §17 (release);
- §18 (Claude documentation);
- D11 and §17 template rules;
- §19 pinned versions.

## Global Constraints

- **Template parameters:** `-n MyApp` replaces `CslaStarter` everywhere (namespaces, projects, cookie name, claim prefix). `--claude` defaults to `true`.
- **What a generated app never contains:** `docs/superpowers/`, `docs/claude/template-dev.md`, `.superpowers/`, `.remember/`, the template's own `.template.config` and `CslaStarter.Templates.csproj`, `scripts/sync-main.sh` and its tests.
- **What `--claude false` also leaves out:** `.claude/`, every `CLAUDE.md`, `docs/claude/`, `.impeccable/`, `sync-main.yml` and `guard-main.yml`.
- **The generated root `CLAUDE.md`:**
  - The Jira epic line becomes `Jira project epic: (set this to your project's epic)`.
  - The template-dev pointer line is removed.
- **E2E tests never run by default.** They need `E2E=1`, Playwright browsers and Docker; the project's `CLAUDE.md` says how to get them. Downloading browsers or images is an explicit step for the person running them, never done by the build.
- **The release workflow** runs on a `v*` tag pushed to `claude`. It attaches the template `.nupkg`, `sbom.cdx.json` and the `efbundle` (linux-x64) to the GitHub release.
- **Claude skills:** `.claude/skills/{new-business-object,new-type,new-component}/SKILL.md`, each stepping through the matching recipe and ending with `dotnet test`.

## Review Focus

1. **`dotnet new csla-blazor -n Acme.Grants --claude false` produces an app with no Claude file at all, and it builds.** *Pinned by* `Template_ClaudeFalse_LeavesNoClaudePaths_AndBuilds` (Task 2).
2. **The generated app never carries this repository's identity:** the Jira epic, the sync scripts, superpowers documents, the template-dev guide. *Pinned by* `Template_GeneratedApp_HasNoTemplateDevArtifacts` (Task 2).
3. **A real sign-in through the mock Entra ID reaches the projects page, and sign-out ends the session.** *Pinned by* `E2E_SignIn_SeesProjects_SignOut_EndsSession` (Task 3).
4. **Two browsers on the same project see each other's saves and presence**, and switching language keeps unsaved edits. *Pinned by* `E2E_TwoBrowsers_LiveUpdateAndPresence`, `E2E_LanguageSwitch_KeepsEdits` (Task 3).
5. **Every sample page has no serious or critical axe violation (WCAG 2.1 AA).** *Pinned by* `E2E_SamplePages_PassAxe` (Task 3).

---

### Task 1: Remaining convention tests

**Files:** `tests/CslaStarter.Architecture.Tests/{MarkupStringTests,SeedConsistencyTests,ClaudePathsOnMainTests}.cs`

- [ ] **Step 1: Write the tests.**
  - `NoMarkupString_OnUserData`: Roslyn finds `MarkupString` and `(MarkupString)`, and only the allow-listed static-resource uses pass.
  - `TypeSeeds_MatchTheConstants`: every `*Type` constant has a seeded row in the migration, and every seeded row has a constant.
  - `ClaudeOnlyPaths_AreExcludedByTheTemplate`: every pattern in `.github/claude-only-paths.txt` is excluded in `template.json` when `claude` is false.
- [ ] **Step 2: Run them.** Any test that passes at once is mutation-checked: break the code it guards and watch it fail.
- [ ] **Step 3: Commit** with the message `DEV-38 Add the remaining convention tests`.

### Task 2: The `dotnet new` template

**Files:**
- `.template.config/template.json`, `.template.config/dotnetcli.host.json`
- `CslaStarter.Templates.csproj` (root; packs the content, honouring the excludes)
- `scripts/tests/template.test.sh` (pack → install into a custom hive → instantiate both variants → check files → `dotnet build` the `--claude false` app when `TEMPLATE_BUILD=1`)
- `README.md` for generated apps (the first-sync step from `workflow.md`, `update-gcds.sh`, bootstrap-admin)

- [ ] **Step 1: Write the failing tests.**
  - `Template_ClaudeTrue_KeepsGuidesAndSkills`.
  - `Template_ClaudeFalse_LeavesNoClaudePaths_AndBuilds`.
  - `Template_GeneratedApp_HasNoTemplateDevArtifacts`.
  - `Template_RenamesEverything`: no `CslaStarter` is left in the generated files except URLs.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.** Root CLAUDE.md lines are replaced through template `modifiers` with the `claude` condition and `replaces` on the epic line.
- [ ] **Step 4: Run the tests.** Expected: PASS (`bash scripts/tests/run.sh`).
- [ ] **Step 5: Commit** with the message `DEV-38 Package the repository as a dotnet new template`.

### Task 3: End-to-end tests

**Files:** `tests/CslaStarter.E2E.Tests/` (`E2EFixture`: PostgreSQL + mock-oauth2-server containers and the app on Kestrel; `axe.min.js` vendored at a pinned version with its licence; `SignInTests`, `LiveTests`, `LanguageTests`, `AccessibilityTests`, `SessionTests`; `CLAUDE.md`)

- [ ] **Step 1: Write the tests** named in Review Focus 3–5, plus `E2E_SessionTimeout_WarnsThenSignsOut`, which uses a 1-minute session configured for tests.
- [ ] **Step 2: Run them.** With `E2E` unset they skip; the person running them sets `E2E=1` after installing browsers.
- [ ] **Step 3: Add the CI job** `e2e` in `ci.yml`, on push to `claude` and nightly: install browsers, then `E2E=1 dotnet test --project tests/CslaStarter.E2E.Tests`.
- [ ] **Step 4: Commit** with the message `DEV-38 Add end-to-end tests with the mock Entra ID and axe`.

### Task 4: Release workflow

**Files:** `.github/workflows/release.yml`, `docs/claude/workflow.md` (release section)

- [ ] **Step 1: Write the workflow.** On a `v*` tag:
  1. Run `build-test`.
  2. `dotnet pack CslaStarter.Templates.csproj -o out`.
  3. `dotnet CycloneDX CslaStarter.slnx -o out -F Json`.
  4. `dotnet ef migrations bundle -p src/CslaStarter.Data -s src/CslaStarter.Data -r linux-x64 -o out/efbundle`.
  5. `gh release create` with the three artifacts.
  6. Actions are pinned to SHAs, like the other workflows.
- [ ] **Step 2: Add a test.** `WorkflowTests.Release_PinsActionsBySha_AndAttachesTheArtifacts` (Architecture.Tests) parses the YAML.
- [ ] **Step 3: Commit** with the message `DEV-38 Add the release workflow`.

### Task 5: Claude skills and final guides

**Files:**
- `.claude/skills/new-business-object/SKILL.md`, `new-type/SKILL.md`, `new-component/SKILL.md`
- `docs/claude/*` (a final pass over every guide for any app made from the template)
- root `CLAUDE.md`
- `ClaudeDocsTests` (skills present when `.claude/` exists)

- [ ] **Step 1: Write the skills** from the recipes.
- [ ] **Step 2: Run `dotnet test`.** Expected: all pass.
- [ ] **Step 3: Commit** with the message `DEV-38 Add Claude skills and complete the guides`.
