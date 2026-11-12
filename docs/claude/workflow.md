# Workflow

## Branches

| Branch | Purpose | Who writes it |
|---|---|---|
| `main` | Clean code without Claude files. Teams and the public see this branch. | Only the `Sync main` workflow |
| `claude` | Integration branch: code plus the Claude guides, specs and plans | Pull requests only, after CI passes |
| `DEV-<n>-<slug>` | One branch per piece of work, cut from `claude` | You |

Steps for a piece of work:

1. `git switch claude && git pull`
2. `git switch -c DEV-<n>-<slug>`
3. Commit. Every message starts with `DEV-<n>`.
4. Push and open a pull request into `claude`. CI (`build-test`) must pass.
5. After the merge, `Sync main` copies the passing commit to `main` and strips the Claude-only paths.

## Sync to main

- `scripts/sync-main.sh <source> <target>` builds `main`'s next commit. It is a single-parent snapshot of the `claude` tree, minus the Claude-only paths. Its message lists the `claude` commits since the previous sync and ends with a `Source-Commit:` trailer.
- `scripts/check-no-claude-files.sh <ref>` fails if any Claude-only file is in `<ref>`. The sync runs it before pushing, and `Guard main` runs it on every push to `main`.
- Both scripts read the pattern list from the commit they work on. A pattern added on `claude` therefore applies to the same sync that first carries those files.
- The sync never moves `main` backwards. Re-running CI on an older `claude` commit reports `up-to-date`.
- GitHub only starts a `workflow_run` workflow from the copy on the default branch (`main`). Two consequences:
  - An edit to `sync-main.yml` on `claude` takes effect from the sync *after* the one that carries it.
  - A new repository has no workflow on `main`, so the first snapshot must be pushed by hand:
    1. Run `bash scripts/sync-main.sh origin/claude origin/main`.
    2. Push the printed SHA to `refs/heads/main` with a temporary write deploy key, because only deploy keys may update `main`.
    3. Delete the temporary key.

## Claude-only paths

`.github/claude-only-paths.txt` is the single list. Supported entries:

- `dir/` matches that folder from the repo root.
- `**/dir/` matches a folder with that name at any depth.
- `**/NAME` matches a file named exactly `NAME` at any depth.

Any other shape stops the sync with an error, so a pattern is never silently ignored. `scripts/claude-paths.sh` also applies a built-in floor (`**/.claude/`, `docs/claude/`, `docs/superpowers/`, `.impeccable/`, `**/CLAUDE.md`, `**/CLAUDE.local.md`) that removing a line can't turn off.

To keep a new path off `main`, add it to the list in the same commit as the files, then run `bash scripts/tests/run.sh`.

## Jira

Work is tracked on the DEV board. The epic is on line 3 of the root `CLAUDE.md`. Each Story's description holds the spec summary, results, follow-ups and session log.
