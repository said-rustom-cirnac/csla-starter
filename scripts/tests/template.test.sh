#!/usr/bin/env bash
# Tests for the dotnet new template (.template.config + template/CslaStarter.Templates.csproj).
# Run through scripts/tests/run.sh. Set TEMPLATE_BUILD=1 to also build a generated app (slow; CI does it).

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="$(cd "$SCRIPTS/.." && pwd)"

fail() { echo "$*" >&2; return 1; }

# Packs the template from the working tree, installs it into a throwaway hive and creates an app (Acme.Grants, or
# $NAME) with the given extra options. Sets $app to the generated app's folder.
generate() {
  work="$(mktemp -d)"
  works+=("$work")
  trap 'rm -rf "${works[@]}"' EXIT
  dotnet pack "$ROOT/template/CslaStarter.Templates.csproj" -o "$work/pkg" --nologo -v q > "$work/pack.log" 2>&1 \
    || { cat "$work/pack.log" >&2; return 1; }
  dotnet new install "$work"/pkg/*.nupkg --debug:custom-hive "$work/hive" > "$work/install.log" 2>&1 \
    || { cat "$work/install.log" >&2; return 1; }
  dotnet new csla-blazor -n "${NAME:-Acme.Grants}" -o "$work/app" --no-restore --debug:custom-hive "$work/hive" "$@" > "$work/new.log" 2>&1 \
    || { cat "$work/new.log" >&2; return 1; }
  app="$work/app"
}

absent() { [[ ! -e "$app/$1" ]] || fail "$1 should not be in the generated app"; }
present() { [[ -e "$app/$1" ]] || fail "$1 should be in the generated app"; }

test_template_claude_true_keeps_guides_and_skills() {
  generate
  present CLAUDE.md
  present docs/claude/architecture.md
  present .claude/settings.json
  present src/Acme.Grants.Business/CLAUDE.md
  grep -q "Jira project epic: (set this to your project's epic)" "$app/CLAUDE.md" || fail "the epic line was not replaced"
  ! grep -q "template-dev" "$app/CLAUDE.md" || fail "the template-dev pointer leaked"
  local leaked
  leaked="$(grep -rnE --exclude-dir=bin --exclude-dir=obj "DEV-3[78]|\bSaid\b|[Pp]lan [0-9]|CSLA Starter" "$app" || true)"
  [[ -z "$leaked" ]] || fail "this repository's identity leaked: $leaked"
  present scripts/sync-main.sh
  present scripts/claude-paths.sh
  (cd "$app" && git init -q && bash scripts/tests/run.sh > /dev/null) || fail "the generated app's script tests fail"
}

# The GC Design System is vendored: the template copies it byte for byte, minified scripts and fonts included.
test_template_copies_the_design_system_unchanged() {
  generate
  diff -r "$ROOT/src/CslaStarter.Components/wwwroot/gcds" "$app/src/Acme.Grants.Components/wwwroot/gcds" > /dev/null \
    || fail "the generated app's wwwroot/gcds differs from the repository's"
}

# Every script a shipped workflow runs ships with it.
test_template_workflows_find_their_scripts() {
  local variant missing
  for variant in true false; do
    generate --claude "$variant"
    missing="$(cd "$app" && grep -ohE "scripts/[A-Za-z0-9_./-]+\.sh" .github/workflows/*.yml | sort -u | while read -r f; do [[ -e "$f" ]] || echo "$f"; done)"
    [[ -z "$missing" ]] || fail "--claude $variant: workflows run missing scripts: $missing"
  done
}

# Tests that only make sense in the template repository never ship (they would read files the template leaves out).
test_template_ships_no_template_only_tests() {
  generate
  local readers
  readers="$(grep -rlE "\.template\.config|template/" "$app/tests" --include=*.cs || true)"
  [[ -z "$readers" ]] || fail "tests that read template-only files: $readers"
}

# Without the Claude workflow there is no claude branch: workflows and Dependabot target main.
test_template_claude_false_targets_main() {
  generate --claude false
  local refs
  refs="$(grep -rnwE "claude" "$app/.github" || true)"
  [[ -z "$refs" ]] || fail "references to the claude branch: $refs"
  grep -q "target-branch: main" "$app/.github/dependabot.yml" || fail "Dependabot must target main"
}

# A name that isn't a valid namespace (or a folder name, the default) still gives matching paths and references.
test_template_hyphenated_name_is_consistent() {
  NAME=acme-grants generate --claude false
  local broken
  broken="$(cd "$app" && for csproj in $(find . -name '*.csproj'); do
      grep -oE 'Include="[^"]+\.csproj"' "$csproj" | sed 's/Include="//; s/"$//; s#\\#/#g' | while read -r ref; do
        [[ -e "$(dirname "$csproj")/$ref" ]] || echo "$csproj -> $ref"
      done
    done)"
  [[ -z "$broken" ]] || fail "project references that don't resolve: $broken"
  local content
  for content in $(grep -rhoE "_content/[A-Za-z0-9_.-]+/" "$app/src" | sort -u | sed 's#_content/##; s#/$##'); do
    [[ -e "$app/src/$content/$content.csproj" ]] || fail "_content/$content/ names no project"
  done
}

test_template_claude_false_leaves_no_claude_paths() {
  generate --claude false
  local leaked
  leaked="$(cd "$app" && find . \( -name CLAUDE.md -o -name CLAUDE.local.md -o -path ./.claude -o -path ./docs/claude -o -path ./.impeccable \) -print)"
  [[ -z "$leaked" ]] || fail "Claude paths left: $leaked"
  absent .github/workflows/sync-main.yml
  absent .github/workflows/guard-main.yml
  absent scripts/sync-main.sh
  absent scripts/claude-paths.sh
  absent scripts/tests/sync-main.test.sh
  present src/Acme.Grants.Web/Program.cs
  if [[ "${TEMPLATE_BUILD:-0}" == 1 ]]; then
    (cd "$app" && dotnet build Acme.Grants.slnx --nologo -v q) || fail "the generated app does not build"
    (cd "$app" && dotnet test --project tests/Acme.Grants.Architecture.Tests --no-build) || fail "the generated app's architecture tests fail"
  fi
}

test_template_has_no_template_dev_artifacts() {
  generate
  absent docs/superpowers
  absent docs/claude/template-dev.md
  absent .template.config
  absent template
  absent scripts/tests/template.test.sh
  absent .superpowers
  absent .remember
}

test_template_renames_everything() {
  generate --claude false
  local left
  left="$(grep -rliE --exclude-dir=bin --exclude-dir=obj "cslastarter|csla-starter|csla_starter" "$app" || true)"
  [[ -z "$left" ]] || fail "the template name is still in: $left"
  grep -q "Database=acme_grants;" "$app/src/Acme.Grants.Web/appsettings.Development.json" || fail "database names must be identifiers"
  present Acme.Grants.slnx
  present src/Acme.Grants.Business/Acme.Grants.Business.csproj
}
