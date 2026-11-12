#!/usr/bin/env bash
# Usage: check-no-claude-files.sh [<ref>]
# Exits 1 and lists every Claude-only file in <ref> (default HEAD); exits 0 when there are none.
# The pattern list is read from <ref> itself, so a pattern added in the same commit still applies.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/claude-paths.sh
source "$here/claude-paths.sh"

ref="${1:-HEAD}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if git cat-file -e "$ref:.github/claude-only-paths.txt" 2> /dev/null; then
  git show "$ref:.github/claude-only-paths.txt" > "$work/paths.txt"
else
  cp "$(git rev-parse --show-toplevel)/.github/claude-only-paths.txt" "$work/paths.txt"
fi
export CLAUDE_PATHS_FILE="$work/paths.txt"

git ls-tree -r -z --name-only "$ref" | claude_paths_filter > "$work/found"
if [[ -s "$work/found" ]]; then
  while IFS= read -r -d '' path; do
    echo "Claude-only file on $ref: $path"
  done < "$work/found"
  exit 1
fi
