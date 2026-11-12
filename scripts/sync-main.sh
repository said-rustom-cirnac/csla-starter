#!/usr/bin/env bash
# Usage: sync-main.sh <source-ref> <target-ref>
# Builds the target branch's next commit: the source tree minus the Claude-only paths, with the target as its
# only parent. Prints the new commit SHA, or "up-to-date" when the target already has that tree. Never pushes.
# The pattern list is read from the source commit, so a pattern added on claude applies to that same sync.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/claude-paths.sh
source "$here/claude-paths.sh"

source_sha="$(git rev-parse --verify "${1:?usage: sync-main.sh <source-ref> <target-ref>}^{commit}")"
target_sha="$(git rev-parse --verify "${2:?usage: sync-main.sh <source-ref> <target-ref>}^{commit}")"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

git show "$source_sha:.github/claude-only-paths.txt" > "$work/paths.txt"
export CLAUDE_PATHS_FILE="$work/paths.txt"

# Build the filtered tree in a throwaway index so the working tree and real index are untouched.
export GIT_INDEX_FILE="$work/index"
git read-tree "$source_sha"
git ls-files -z | claude_paths_filter > "$work/strip"
if [[ -s "$work/strip" ]]; then
  GIT_LITERAL_PATHSPECS=1 git rm --cached -q --pathspec-from-file="$work/strip" --pathspec-file-nul
fi
tree="$(git write-tree)"
unset GIT_INDEX_FILE

if [[ "$tree" == "$(git rev-parse "$target_sha^{tree}")" ]]; then
  echo up-to-date
  exit 0
fi

previous="$(git log -1 --format=%B "$target_sha" | sed -n 's/^Source-Commit: //p' | tail -n 1)"

# Never move main backwards: a re-run of an older claude commit's CI must not replace a newer snapshot.
if [[ -n "$previous" ]] && git merge-base --is-ancestor "$source_sha" "$previous" 2> /dev/null; then
  echo up-to-date
  exit 0
fi

range="$source_sha"
if [[ -n "$previous" ]]; then
  range="$previous..$source_sha"
fi

{
  printf 'Sync from claude %s\n\n' "$(git rev-parse --short "$source_sha")"
  git log --format='- %s' "$range"
  printf '\nSource-Commit: %s\n' "$source_sha"
} > "$work/message.txt"

new_sha="$(git commit-tree "$tree" -p "$target_sha" -F "$work/message.txt")"

if ! "$here/check-no-claude-files.sh" "$new_sha" >&2; then
  echo "sync-main: Claude-only files survived filtering; nothing was synced." >&2
  exit 2
fi

echo "$new_sha"
