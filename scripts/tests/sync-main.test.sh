#!/usr/bin/env bash
# Tests for sync-main.sh and check-no-claude-files.sh. Run through scripts/tests/run.sh.

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="$(cd "$SCRIPTS/.." && pwd)"

fail() { echo "$*" >&2; return 1; }

# A throwaway repo: main has a root commit; claude adds Claude-only files at several depths plus lookalikes.
new_repo() {
  repo="$(mktemp -d)"
  trap 'rm -rf "$repo"' EXIT
  cd "$repo" || return 1
  git init -q -b main
  git config user.email test@example.com
  git config user.name test
  git config commit.gpgsign false
  echo readme > README.md
  git add -A && git commit -qm "root"
  git switch -qc claude
  mkdir -p .github && cp "$ROOT/.github/claude-only-paths.txt" .github/
  local f
  for f in src/A/a.cs src/A/CLAUDE.md CLAUDE.md tests/B/CLAUDE.md docs/claude/guide.md \
           docs/superpowers/specs/s.md .claude/settings.json .impeccable/config.json \
           docs/claude-notes.md CLAUDE.md.txt src/NOTCLAUDE.md; do
    mkdir -p "$(dirname "$f")" && echo "$f" > "$f"
  done
  git add -A && git commit -qm "DEV-1 add files"
}

sync_once() { "$SCRIPTS/sync-main.sh" claude main; }

# Sync and move main to the new commit, as the workflow's push would.
apply_sync() {
  local sha
  sha="$(sync_once)"
  [[ "$sha" == up-to-date ]] || git update-ref refs/heads/main "$sha"
  echo "$sha"
}

tree_of() { git -c core.quotePath=false ls-tree -r --name-only "$1"; }

test_sync_strips_claude_paths_at_any_depth() {
  new_repo
  local tree p
  tree="$(tree_of "$(sync_once)")"
  for p in CLAUDE.md src/A/CLAUDE.md tests/B/CLAUDE.md docs/claude/guide.md \
           docs/superpowers/specs/s.md .claude/settings.json .impeccable/config.json; do
    if grep -qxF "$p" <<< "$tree"; then fail "$p survived the sync"; fi
  done
}

test_sync_keeps_lookalike_names() {
  new_repo
  local tree p
  tree="$(tree_of "$(sync_once)")"
  for p in docs/claude-notes.md CLAUDE.md.txt src/NOTCLAUDE.md; do
    grep -qxF "$p" <<< "$tree" || fail "$p was wrongly stripped"
  done
}

test_sync_copies_non_claude_files_exactly() {
  new_repo
  local sha p
  sha="$(sync_once)"
  [[ "$(tree_of "$sha" | wc -l)" -eq 6 ]] || fail "expected 6 files, got: $(tree_of "$sha")"
  while IFS= read -r p; do
    [[ "$(git rev-parse "$sha:$p")" == "$(git rev-parse "claude:$p")" ]] || fail "$p differs from claude"
  done < <(tree_of "$sha")
}

test_sync_removes_files_deleted_on_claude() {
  new_repo
  apply_sync > /dev/null
  git rm -q src/A/a.cs && git commit -qm "DEV-1 delete a.cs"
  apply_sync > /dev/null
  if grep -qxF src/A/a.cs <<< "$(tree_of main)"; then fail "deleted file still on main"; fi
}

test_sync_is_idempotent() {
  new_repo
  apply_sync > /dev/null
  local before
  before="$(git rev-list --count main)"
  [[ "$(sync_once)" == up-to-date ]] || fail "second sync did not report up-to-date"
  [[ "$(git rev-list --count main)" == "$before" ]] || fail "main gained a commit"
}

test_sync_commit_has_single_parent_and_trailer() {
  new_repo
  local old sha
  old="$(git rev-parse main)"
  sha="$(sync_once)"
  [[ "$(git rev-list --parents -n1 "$sha")" == "$sha $old" ]] || fail "expected exactly one parent ($old)"
  git log -1 --format=%B "$sha" | grep -qxF "Source-Commit: $(git rev-parse claude)" || fail "missing Source-Commit trailer"
}

test_sync_body_lists_only_new_claude_subjects() {
  new_repo
  apply_sync > /dev/null
  echo b > src/A/b.cs && git add -A && git commit -qm "DEV-2 second change"
  local body
  body="$(git log -1 --format=%B "$(apply_sync)")"
  grep -qxF -- "- DEV-2 second change" <<< "$body" || fail "new subject missing: $body"
  if grep -qF "DEV-1 add files" <<< "$body"; then fail "already-synced subject repeated: $body"; fi
}

test_sync_uses_source_branch_path_list() {
  new_repo
  echo ".newtool/" >> .github/claude-only-paths.txt
  mkdir .newtool && echo y > .newtool/x
  git add -A && git commit -qm "DEV-3 add a Claude-only tool folder"
  local tree
  tree="$(tree_of "$(sync_once)")"
  grep -qxF README.md <<< "$tree" || fail "sync produced no usable tree"
  if grep -q '^\.newtool/' <<< "$tree"; then fail ".newtool/ leaked to main"; fi
}

test_sync_handles_accented_file_names() {
  new_repo
  echo z > "docs/claude/étape.md" && echo w > "src/Résumé.cs"
  git add -A && git commit -qm "DEV-4 accented names"
  local tree
  tree="$(tree_of "$(sync_once)")"
  grep -qxF "src/Résumé.cs" <<< "$tree" || fail "accented non-Claude file missing"
  if grep -qF "étape" <<< "$tree"; then fail "accented Claude file leaked"; fi
}

test_guard_fails_on_nested_claude_md() {
  new_repo
  local out
  if out="$("$SCRIPTS/check-no-claude-files.sh" claude)"; then fail "guard passed on claude"; fi
  grep -qxF "Claude-only file on claude: src/A/CLAUDE.md" <<< "$out" || fail "guard output: $out"
}

test_guard_passes_on_synced_main() {
  new_repo
  apply_sync > /dev/null
  "$SCRIPTS/check-no-claude-files.sh" main
}

test_sync_ignores_older_source_commit() {
  new_repo
  apply_sync > /dev/null
  local older
  older="$(git rev-parse claude)"
  echo two > src/A/a.cs && git add -A && git commit -qm "DEV-5 newer change"
  apply_sync > /dev/null
  local before
  before="$(git rev-parse main)"
  [[ "$("$SCRIPTS/sync-main.sh" "$older" main)" == up-to-date ]] || fail "an older claude commit was synced over a newer one"
  [[ "$(git rev-parse main)" == "$before" ]] || fail "main moved"
}

test_sync_strips_names_needing_quotes() {
  new_repo
  mkdir -p 'x"y'
  echo q > 'docs/claude/a"b.md' && printf 't\n' > "docs/claude/tab$(printf '\t')name.md"
  echo c > 'x"y/CLAUDE.md' && echo k > 'src/q"uote.cs'
  git add -A && git commit -qm "DEV-6 names that git quotes"
  local sha
  sha="$(sync_once)"
  if git ls-tree -r -z --name-only "$sha" | tr '\0' '\n' | grep -qE '^docs/claude/|(^|/)CLAUDE\.md$'; then fail "a quoted Claude path leaked"; fi
  git ls-tree -r -z --name-only "$sha" | tr '\0' '\n' | grep -qxF 'src/q"uote.cs' || fail "a quoted non-Claude path was dropped"
}

test_sync_strips_nested_claude_folders_and_local_md() {
  new_repo
  mkdir -p src/Web/.claude
  echo s > src/Web/.claude/settings.json && echo l > CLAUDE.local.md && echo l > src/A/CLAUDE.local.md
  git add -A && git commit -qm "DEV-7 nested Claude files"
  local tree p
  tree="$(tree_of "$(sync_once)")"
  for p in src/Web/.claude/settings.json CLAUDE.local.md src/A/CLAUDE.local.md; do
    if grep -qxF "$p" <<< "$tree"; then fail "$p survived the sync"; fi
  done
}

test_sync_fails_on_unsupported_pattern() {
  new_repo
  echo 'docs/*.tmp' >> .github/claude-only-paths.txt
  git add -A && git commit -qm "DEV-8 a pattern the filter cannot read"
  local out
  if out="$(sync_once 2>&1)"; then fail "sync accepted an unsupported pattern: $out"; fi
  grep -qF "unsupported pattern" <<< "$out" || fail "no clear error: $out"
}

test_guard_keeps_builtin_patterns_when_list_is_emptied() {
  new_repo
  echo '# emptied' > .github/claude-only-paths.txt
  git add -A && git commit -qm "DEV-9 empty the list"
  local out
  if out="$("$SCRIPTS/check-no-claude-files.sh" claude)"; then fail "guard passed with an emptied list"; fi
  grep -qxF "Claude-only file on claude: src/A/CLAUDE.md" <<< "$out" || fail "guard output: $out"
}
