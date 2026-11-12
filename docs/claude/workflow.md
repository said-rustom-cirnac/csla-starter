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

## Claude-only paths

`.github/claude-only-paths.txt` is the single list. A `dir/` entry matches that folder from the repo root; a `**/NAME` entry matches a file named exactly `NAME` at any depth. To keep a new path off `main`, add it to this file in the same commit as the files, then run `bash scripts/tests/run.sh`.

## Jira

Work is tracked on the DEV board. The epic is on line 3 of the root `CLAUDE.md`. Each Story's description holds the spec summary, results, follow-ups and session log.
