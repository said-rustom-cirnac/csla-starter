#!/usr/bin/env bash
# Fetches the GC Design System web components (pinned) into src/CslaStarter.Components/wwwroot/gcds, so the app
# serves them itself (no CDN under Protected B). npm checks the package against the registry's integrity hash; this
# script also checks it against scripts/gcds.integrity, which pins the hash the first run recorded.
#
# Usage: scripts/update-gcds.sh            (needs Node.js and network access to registry.npmjs.org)
set -euo pipefail

readonly COMPONENTS_VERSION="1.7.0"
readonly PACKAGE="@cdssnc/gcds-components"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dest="$root/src/CslaStarter.Components/wwwroot/gcds"
pin="$root/scripts/gcds.integrity"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

cd "$work"
npm pack "$PACKAGE@$COMPONENTS_VERSION" --json --silent > pack.json
tarball="$(node -e 'console.log(require("./pack.json")[0].filename)')"
integrity="$(node -e 'console.log(require("./pack.json")[0].integrity)')"

if [[ -f "$pin" ]]; then
    expected="$(cat "$pin")"
    if [[ "$integrity" != "$expected" ]]; then
        echo "Integrity mismatch for $PACKAGE@$COMPONENTS_VERSION: got $integrity, pinned $expected" >&2
        exit 1
    fi
else
    echo "$integrity" > "$pin"
    echo "Pinned $PACKAGE@$COMPONENTS_VERSION integrity in scripts/gcds.integrity; commit it."
fi

tar -xzf "$tarball"
rm -rf "$dest"
mkdir -p "$dest"
cp -R package/dist package/loader "$dest/"
[[ -f package/LICENSE ]] && cp package/LICENSE "$dest/"
echo "$COMPONENTS_VERSION" > "$dest/VERSION"
echo "GC Design System $COMPONENTS_VERSION is in ${dest#"$root/"}."
