# Developing the template

This file is about building the CSLA Starter template itself. The `dotnet new` template always leaves it out of generated apps.

## Where the design lives

- Design spec: [2026-11-12-csla-starter-design.md](../superpowers/specs/2026-11-12-csla-starter-design.md)
- Director-facing summary: `docs/architecture/csla-starter-architecture.html`
- Plan roadmap: [2026-11-12-roadmap.md](../superpowers/plans/2026-11-12-roadmap.md). There is one plan per phase, written when the previous phase is merged.

## The template

- **Files:** `.template.config/template.json` (`dotnet new csla-blazor`; the `safeName` symbol replaces `CslaStarter` in names, paths and content) and `template/CslaStarter.Templates.csproj`, which packs the repository as `content/`. Test locally with `bash scripts/tests/run.sh`: it generates apps (including a hyphenated name), checks that every script a shipped workflow runs is there, and runs the generated app's script tests. `TEMPLATE_BUILD=1` also builds a generated app and runs its architecture tests, as CI does.
- **Never in a generated app:** `docs/superpowers/`, this file, `docs/architecture/`, `.superpowers/`, `.remember/`, the template's own files (including `TemplateRepositoryTests.cs` and `template.test.sh`), lock files (the restore post-action writes fresh ones), and local caches (`*.cache.json`).
- **Only with `--claude true`:** the guides and skills, and the `claude` → `main` sync: `sync-main.yml`, `guard-main.yml`, `sync-main.sh`, `claude-paths.sh`, `check-no-claude-files.sh` and their tests. Without it, the workflows and Dependabot target `main` (switch symbols in `template.json`).
- **Names:** the app name's safe-namespace form (`acme-grants` becomes `acme_grants`) is used for files, projects and namespaces alike, so paths and references agree. The package drops the executable bit, so docs and workflows run scripts with `bash`.
- **`--claude false` also drops** `.claude/`, every `CLAUDE.md`, `docs/claude/`, `.impeccable/`, `sync-main.yml`, `guard-main.yml`, `claude-only-paths.txt` and `check-no-claude-files.sh`.
- **The root `CLAUDE.md`:** its Jira epic line becomes a placeholder and its pointer to this file disappears (constant symbols in `template.json`).
- **`README.app.md`** becomes the app's `README.md`.
- **Name forms.** `-n Acme.Grants` gives:
  - `Acme.Grants` in namespaces and paths;
  - `AcmeGrants` inside `Add…`/`Use…`/`Map…` identifiers (`onlyIf`);
  - `acme_grants` for databases and claim prefixes;
  - `acme-grants` for the compose project and OIDC client id;
  - `ACME_GRANTS` for environment variables.

  **Never embed `CslaStarter` in any other identifier:** add a form for it first, or the generated app won't build (the template test catches it).
- **A new app's repository** needs the one-time first sync from `workflow.md`, because `sync-main.yml` only starts from `main`. The app's README says so.

## Repository settings

- **`protect-main` ruleset:** only the `sync-main` deploy key may update `main`.
- **`protect-claude` ruleset:** changes need a pull request, and `build-test` must pass.
- The sync deploy key's private half is the Actions secret `SYNC_DEPLOY_KEY`.
