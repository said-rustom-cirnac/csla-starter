# Developing the template

This file is about building the CSLA Starter template itself. The `dotnet new` template always leaves it out of generated apps.

## Where the design lives

- Design spec: [2026-11-12-csla-starter-design.md](../superpowers/specs/2026-11-12-csla-starter-design.md)
- Director-facing summary: `docs/architecture/csla-starter-architecture.html`
- Plan roadmap: [2026-11-12-roadmap.md](../superpowers/plans/2026-11-12-roadmap.md). There is one plan per phase, written when the previous phase is merged.

## Things the template packaging (plan 6) must handle

- The Jira epic line in the root `CLAUDE.md` is specific to this repository. The template must replace it or drop it.
- `docs/superpowers/` and this file are about the template. Generated apps never get them, even with `--claude`.
- With `--claude false`, the template also leaves out `.claude/`, every `CLAUDE.md`, `docs/claude/`, `.impeccable/`, `sync-main.yml` and `guard-main.yml`.
- Guides under `docs/claude/` must describe any app made from the template, not this repository's history.

## Repository settings

- **`protect-main` ruleset:** only the `sync-main` deploy key may update `main`.
- **`protect-claude` ruleset:** changes need a pull request, and `build-test` must pass.
- The sync deploy key's private half is the Actions secret `SYNC_DEPLOY_KEY`.
