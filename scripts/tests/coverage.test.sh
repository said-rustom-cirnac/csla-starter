#!/usr/bin/env bash
# Tests for scripts/check-coverage.sh. Run through scripts/tests/run.sh.

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

fail() { echo "$*" >&2; return 1; }

# Writes a Cobertura report with the given "package line-rate" pairs to $report.
report() {
  report="$(mktemp)"
  {
    echo '<?xml version="1.0" encoding="utf-8"?><coverage line-rate="0.5"><packages>'
    while (($#)); do echo "<package name=\"$1\" line-rate=\"$2\" branch-rate=\"0\"/>"; shift 2; done
    echo '</packages></coverage>'
  } > "$report"
}

test_coverage_passes_at_the_minimum() {
  report App.Business 0.80 App.Core 0.10
  bash "$SCRIPTS/check-coverage.sh" 80 App.Business "$report" > /dev/null || fail "80% of lines should pass an 80% minimum"
  rm -f "$report"
}

test_coverage_fails_below_the_minimum_and_says_where() {
  report App.Business 0.799
  local out
  if out="$(bash "$SCRIPTS/check-coverage.sh" 80 App.Business "$report" 2>&1)"; then fail "79.9% passed an 80% minimum"; fi
  grep -q "App.Business" <<< "$out" && grep -q "79.9" <<< "$out" || fail "the failure should name the project and its coverage: $out"
  rm -f "$report"
}

test_coverage_checks_every_pair() {
  report App.Business 0.95
  local good="$report"
  report App.Components 0.50
  if bash "$SCRIPTS/check-coverage.sh" 80 App.Business "$good" App.Components "$report" > /dev/null 2>&1; then
    fail "the second project's 50% passed"
  fi
  rm -f "$good" "$report"
}

test_coverage_fails_when_the_project_is_not_in_the_report() {
  report App.Core 0.99
  if bash "$SCRIPTS/check-coverage.sh" 80 App.Business "$report" > /dev/null 2>&1; then fail "a missing project passed"; fi
  rm -f "$report"
}
