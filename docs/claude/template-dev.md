# Developing the template

This file is about building the CSLA Starter template itself. The `dotnet new` template always leaves it out of generated apps.

## Where the design lives

- Design spec: [2026-11-12-csla-starter-design.md](../superpowers/specs/2026-11-12-csla-starter-design.md)
- Director-facing summary: `docs/architecture/csla-starter-architecture.html`
- Plan roadmap: [2026-11-12-roadmap.md](../superpowers/plans/2026-11-12-roadmap.md). There is one plan per phase, written when the previous phase is merged.

## The template

- **Files:** `.template.config/template.json` (`dotnet new csla-blazor`, `sourceName: CslaStarter`) and `template/CslaStarter.Templates.csproj`, which packs the repository as `content/`. Test locally with `bash scripts/tests/run.sh`; `TEMPLATE_BUILD=1` also builds a generated app, as CI does.
- **Never in a generated app:** `docs/superpowers/`, this file, `docs/architecture/`, `.superpowers/`, `.remember/`, the template's own files, `scripts/sync-main.sh`, `claude-paths.sh` and their tests, lock files (the restore post-action writes fresh ones), and local caches (`*.cache.json`).
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
