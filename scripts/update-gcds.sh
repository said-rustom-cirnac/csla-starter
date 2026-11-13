#!/usr/bin/env bash
# Fetches the GC Design System (pinned) into src/CslaStarter.Components/wwwroot/gcds, so the app serves it itself (no
# CDN under Protected B): the web components, and the fonts that gcds.css would otherwise import from Google Fonts.
# npm checks each package against the registry's integrity hash; this script also checks it against
# scripts/gcds.integrity, which pins the hashes the first run recorded ("package@version sha512-..." per line).
#
# Usage: scripts/update-gcds.sh            (needs Node.js and network access to registry.npmjs.org)
set -euo pipefail

readonly COMPONENTS="@gcds-core/components"
readonly COMPONENTS_VERSION="1.7.0"
readonly FONTS="@gcds-core/fonts"
readonly FONTS_VERSION="1.1.1"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dest="$root/src/CslaStarter.Components/wwwroot/gcds"
pins="$root/scripts/gcds.integrity"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Packs one package into $work/<dir>/package and checks its integrity against the pin (recording it on the first run).
fetch() {
    local package="$1" version="$2" dir="$work/$3"
    mkdir -p "$dir"
    (cd "$dir" && npm pack "$package@$version" --json --silent > pack.json)
    local tarball integrity pinned
    tarball="$(node -e 'console.log(require(process.argv[1])[0].filename)' "$dir/pack.json")"
    integrity="$(node -e 'console.log(require(process.argv[1])[0].integrity)' "$dir/pack.json")"
    pinned="$( (grep -F "$package@$version " "$pins" 2>/dev/null || true) | cut -d' ' -f2)"
    if [[ -z "$pinned" ]]; then
        echo "$package@$version $integrity" >> "$pins"
        echo "Pinned $package@$version integrity in scripts/gcds.integrity; commit it."
    elif [[ "$integrity" != "$pinned" ]]; then
        echo "Integrity mismatch for $package@$version: got $integrity, pinned $pinned" >&2
        exit 1
    fi

    tar -xzf "$dir/$tarball" -C "$dir"
}

fetch "$COMPONENTS" "$COMPONENTS_VERSION" components
fetch "$FONTS" "$FONTS_VERSION" fonts

rm -rf "$dest"
mkdir -p "$dest/fonts"
cp -R "$work/components/package/dist" "$work/components/package/loader" "$dest/"
[[ -f "$work/components/package/LICENSE" ]] && cp "$work/components/package/LICENSE" "$dest/"

# The fonts: the package's files (not its npm metadata), behind one stylesheet the app links, gcds/fonts.css.
cp -R "$work/fonts/package/." "$dest/fonts/"
rm -f "$dest/fonts/package.json" "$dest/fonts/README.md"
mapfile -t sheets < <(cd "$dest" && find fonts -name '*.min.css' | sort)
if (( ${#sheets[@]} == 0 )); then
    mapfile -t sheets < <(cd "$dest" && find fonts -name '*.css' | sort)
fi
if (( ${#sheets[@]} == 0 )); then
    echo "$FONTS@$FONTS_VERSION has no stylesheet; check the package layout." >&2
    exit 1
fi
printf '@import url("%s");\n' "${sheets[@]}" > "$dest/fonts.css"

# gcds.css imports its fonts from Google Fonts, which the CSP (and Protected B) rule out: drop the import.
css="$dest/dist/gcds/gcds.css"
if [[ ! -f "$css" ]]; then
    echo "Expected dist/gcds/gcds.css in $COMPONENTS@$COMPONENTS_VERSION; check the package layout." >&2
    exit 1
fi
perl -0pi -e 's/\@import\s+url\(\s*["\x27]?https:\/\/fonts\.googleapis\.com[^)]*\)\s*;?//g' "$css"
if grep -Eq 'fonts\.(googleapis|gstatic)\.com' "$css"; then
    echo "gcds.css still loads fonts from Google; update this script." >&2
    exit 1
fi

echo "$COMPONENTS_VERSION" > "$dest/VERSION"
echo "GC Design System $COMPONENTS_VERSION (fonts $FONTS_VERSION) is in ${dest#"$root/"}; review fonts.css."
