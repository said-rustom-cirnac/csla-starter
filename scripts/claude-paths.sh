#!/usr/bin/env bash
# Sourced helper. claude_paths_filter reads NUL-separated repo-relative paths on stdin and writes the
# Claude-only ones, NUL-separated, to stdout. It returns 1 on a pattern it cannot read, so callers stop
# instead of leaking files.
#
# Patterns come from the built-in floor below plus $CLAUDE_PATHS_FILE
# (default: .github/claude-only-paths.txt in the current repo):
#   "dir/"      that folder, from the repo root
#   "**/dir/"   a folder with that name at any depth
#   "**/NAME"   a file named exactly NAME at any depth
# Removing a line from the file never removes a built-in pattern.

CLAUDE_PATHS_BUILTIN=('**/.claude/' 'docs/claude/' 'docs/superpowers/' '.impeccable/' '**/CLAUDE.md' '**/CLAUDE.local.md')

claude_paths_filter() {
  local file="${CLAUDE_PATHS_FILE:-$(git rev-parse --show-toplevel)/.github/claude-only-paths.txt}"
  local -a root_dirs=() any_dirs=() names=() patterns=("${CLAUDE_PATHS_BUILTIN[@]}")
  local line pattern rest path

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%%#*}"
    line="${line//[[:space:]]/}"
    [[ -n "$line" ]] && patterns+=("$line")
  done < "$file"

  for pattern in "${patterns[@]}"; do
    rest="${pattern#'**/'}"
    if [[ "$rest" == *[\*\?\[]* || -z "$rest" || "$rest" == /* ]]; then
      echo "claude-paths: unsupported pattern '$pattern' (use dir/, **/dir/ or **/NAME)" >&2
      return 1
    elif [[ "$pattern" == '**/'* && "$rest" == */ && "${rest%/}" != */* ]]; then
      any_dirs+=("/$rest")
    elif [[ "$pattern" == '**/'* && "$rest" != */* ]]; then
      names+=("$rest")
    elif [[ "$pattern" != '**/'* && "$pattern" == */ ]]; then
      root_dirs+=("$pattern")
    else
      echo "claude-paths: unsupported pattern '$pattern' (use dir/, **/dir/ or **/NAME)" >&2
      return 1
    fi
  done

  while IFS= read -r -d '' path; do
    for pattern in "${root_dirs[@]}"; do
      if [[ "$path" == "$pattern"* ]]; then printf '%s\0' "$path"; continue 2; fi
    done
    for pattern in "${any_dirs[@]}"; do
      if [[ "/$path" == *"$pattern"* ]]; then printf '%s\0' "$path"; continue 2; fi
    done
    for pattern in "${names[@]}"; do
      if [[ "${path##*/}" == "$pattern" ]]; then printf '%s\0' "$path"; continue 2; fi
    done
  done
}
