#!/usr/bin/env bash
# Sourced helper. claude_paths_filter reads repo-relative paths on stdin and prints the Claude-only ones.
# Patterns come from $CLAUDE_PATHS_FILE (default: .github/claude-only-paths.txt in the current repo):
#   "dir/"     matches that directory prefix from the repo root
#   "**/NAME"  matches a file whose name is exactly NAME, at any depth

claude_paths_filter() {
  local file="${CLAUDE_PATHS_FILE:-$(git rev-parse --show-toplevel)/.github/claude-only-paths.txt}"
  local -a prefixes=() names=()
  local line path pattern

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%%#*}"
    line="${line//[[:space:]]/}"
    [[ -z "$line" ]] && continue
    if [[ "$line" == '**/'* ]]; then
      names+=("${line#'**/'}")
    else
      prefixes+=("$line")
    fi
  done < "$file"

  while IFS= read -r path; do
    for pattern in "${prefixes[@]}"; do
      if [[ "$path" == "$pattern"* ]]; then printf '%s\n' "$path"; continue 2; fi
    done
    for pattern in "${names[@]}"; do
      if [[ "${path##*/}" == "$pattern" ]]; then printf '%s\n' "$path"; continue 2; fi
    done
  done
}
