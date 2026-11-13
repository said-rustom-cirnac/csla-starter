#!/usr/bin/env bash
# Tests for the dotnet new template (.template.config + template/CslaStarter.Templates.csproj).
# Run through scripts/tests/run.sh. Set TEMPLATE_BUILD=1 to also build a generated app (slow; CI does it).

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="$(cd "$SCRIPTS/.." && pwd)"

fail() { echo "$*" >&2; return 1; }

# Packs the template from the working tree, installs it into a throwaway hive and creates Acme.Grants with the
# given extra options. Sets $app to the generated app's folder.
generate() {
  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT
  dotnet pack "$ROOT/template/CslaStarter.Templates.csproj" -o "$work/pkg" --nologo -v q > "$work/pack.log" 2>&1 \
    || { cat "$work/pack.log" >&2; return 1; }
  dotnet new install "$work"/pkg/*.nupkg --debug:custom-hive "$work/hive" > "$work/install.log" 2>&1 \
    || { cat "$work/install.log" >&2; return 1; }
  dotnet new csla-blazor -n Acme.Grants -o "$work/app" --no-restore --debug:custom-hive "$work/hive" "$@" > "$work/new.log" 2>&1 \
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
  ! grep -q "DEV-37" "$app/CLAUDE.md" || fail "this repository's epic leaked"
  ! grep -q "template-dev" "$app/CLAUDE.md" || fail "the template-dev pointer leaked"
}

test_template_claude_false_leaves_no_claude_paths() {
  generate --claude false
  local leaked
  leaked="$(cd "$app" && find . \( -name CLAUDE.md -o -name CLAUDE.local.md -o -path ./.claude -o -path ./docs/claude -o -path ./.impeccable \) -print)"
  [[ -z "$leaked" ]] || fail "Claude paths left: $leaked"
  absent .github/workflows/sync-main.yml
  absent .github/workflows/guard-main.yml
  present src/Acme.Grants.Web/Program.cs
  if [[ "${TEMPLATE_BUILD:-0}" == 1 ]]; then
    (cd "$app" && dotnet build Acme.Grants.slnx --nologo -v q) || fail "the generated app does not build"
  fi
}

test_template_has_no_template_dev_artifacts() {
  generate
  absent docs/superpowers
  absent docs/claude/template-dev.md
  absent .template.config
  absent template
  absent scripts/sync-main.sh
  absent scripts/tests/sync-main.test.sh
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
