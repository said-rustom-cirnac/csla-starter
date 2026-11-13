#!/usr/bin/env bash
# Fetches the GC Design System (pinned) into src/CslaStarter.Components/wwwroot/gcds, so the app serves it itself (no
# CDN under Protected B): the web components, and the fonts that gcds.css would otherwise load from Google Fonts and
# the design system's CDN.
# npm checks each package against the registry's integrity hash; this script also checks it against
# scripts/gcds.integrity, which pins the hashes the first run recorded ("package@version sha512-..." per line).
#
# Its output is committed, so run it only to move to a new version: change the pins below, run it, review and commit.
# Usage: bash scripts/update-gcds.sh       (needs Node.js and network access to registry.npmjs.org)
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
mkdir -p "$dest/dist/gcds" "$dest/loader" "$dest/fonts"

# Only what the app loads: the loader, the ES module build it imports, and gcds.css. The package's other builds
# (CommonJS, custom elements, the lazy www bundle) and its type definitions would only add weight to every app.
package="$work/components/package"
for file in "$package/loader/index.js" "$package/dist/esm/loader.js" "$package/dist/gcds/gcds.css"; do
    if [[ ! -f "$file" ]]; then
        echo "Expected ${file#"$package/"} in $COMPONENTS@$COMPONENTS_VERSION; check the package layout." >&2
        exit 1
    fi
done
cp "$package/loader/index.js" "$dest/loader/"
cp -R "$package/dist/esm" "$dest/dist/"
cp "$package/dist/gcds/gcds.css" "$dest/dist/gcds/"
[[ -f "$package/LICENSE" ]] && cp "$package/LICENSE" "$dest/"

# The fonts: each family's files and licence, behind one stylesheet the app links, gcds/fonts.css. The package's
# stylesheets ship with placeholder paths ("path/to/file/"); they sit beside their fonts, so the paths become relative.
if [[ ! -d "$work/fonts/package/fonts" ]]; then
    echo "Expected a fonts folder in $FONTS@$FONTS_VERSION; check the package layout." >&2
    exit 1
fi
cp -R "$work/fonts/package/fonts/." "$dest/fonts/"
find "$dest/fonts" -name 'README.md' -delete
mapfile -t sheets < <(cd "$dest" && find fonts -name '*.css' | LC_ALL=C sort)
if (( ${#sheets[@]} == 0 )); then
    echo "$FONTS@$FONTS_VERSION has no stylesheet; check the package layout." >&2
    exit 1
fi
for sheet in "${sheets[@]}"; do
    perl -pi -e 's#url\(\s*(["\x27]?)(?:<path-to-file>|path/to/file)/#url($1#g' "$dest/$sheet"
done
printf '@import url("%s");\n' "${sheets[@]}" > "$dest/fonts.css"

# gcds.css imports its fonts from Google Fonts and its icon font from the design system's CDN, which the CSP (and
# Protected B) rule out: drop the import and point the icon font at the local copy.
css="$dest/dist/gcds/gcds.css"
perl -0pi -e 's/\@import\s+url\(\s*["\x27]?https:\/\/fonts\.googleapis\.com[^)]*\)\s*;?//g' "$css"
perl -pi -e 's#https://cdn\.design-system\.canada\.ca/\@gcds-core/fonts\@[^/]+/#../../fonts/#g' "$css"
leftover='url\(\s*["\x27]?((https?:)?//|<path-to-file>|path/to/file)[^)]*\)'
if grep -rPq --include='*.css' "$leftover" "$dest"; then
    echo "A stylesheet still loads from another host or a placeholder path; update this script:" >&2
    grep -rPno --include='*.css' "$leftover" "$dest" >&2
    exit 1
fi

# VERSION.txt, not VERSION: NuGet packs a file with no extension as a folder, which would break the template.
echo "$COMPONENTS_VERSION" > "$dest/VERSION.txt"
echo "GC Design System $COMPONENTS_VERSION (fonts $FONTS_VERSION) is in ${dest#"$root/"}; review fonts.css."
