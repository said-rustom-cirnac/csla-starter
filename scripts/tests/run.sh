#!/usr/bin/env bash
# Runs every test_* function in scripts/tests/*.test.sh, each in its own shell.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
passed=0
failed=0

for file in "$dir"/*.test.sh; do
  names="$(bash -c "source '$file'; declare -F" | awk '{print $3}' | grep '^test_' || true)"
  for name in $names; do
    if output="$(bash -c "set -euo pipefail; source '$file'; $name" 2>&1)"; then
      echo "PASS $name"
      passed=$((passed + 1))
    else
      echo "FAIL $name"
      printf '%s\n' "$output" | sed 's/^/    /'
      failed=$((failed + 1))
    fi
  done
done

echo "$passed passed, $failed failed"
[[ $failed -eq 0 ]]
