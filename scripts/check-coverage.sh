#!/usr/bin/env bash
# Fails unless each project's line coverage, read from a Cobertura report, is at least the minimum (spec §16: 80% for
# Business and Components). CI writes the reports with dotnet test --coverage --coverage-output-format cobertura.
#
# Usage: scripts/check-coverage.sh <minimum %> <project> <report.cobertura.xml> [<project> <report> ...]
set -euo pipefail

if (( $# < 3 || $# % 2 == 0 )); then
  echo "usage: $0 <minimum %> <project> <report> [<project> <report> ...]" >&2
  exit 2
fi

python3 - "$@" <<'PY'
import sys
import xml.etree.ElementTree as ET

minimum = float(sys.argv[1])
failed = False
pairs = sys.argv[2:]
for project, report in zip(pairs[::2], pairs[1::2]):
    rates = [float(p.get("line-rate", 0)) for p in ET.parse(report).getroot().iter("package") if p.get("name") == project]
    if not rates:
        print(f"{project}: not in {report}", file=sys.stderr)
        failed = True
        continue
    covered = rates[0] * 100
    ok = covered + 1e-9 >= minimum
    print(f"{project}: {covered:.1f}% of lines (minimum {minimum:g}%)", file=sys.stdout if ok else sys.stderr)
    failed |= not ok
sys.exit(1 if failed else 0)
PY
