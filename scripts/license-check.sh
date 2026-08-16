#!/usr/bin/env bash
# License gate: every NuGet dependency (csproj PackageReference + tool manifest)
# must appear, with its exact version, in docs/licenses-allowlist.tsv.
# A new or bumped dependency is a conscious, reviewed decision — never a drive-by.
set -euo pipefail
cd "$(dirname "$0")/.."

ALLOWLIST=docs/licenses-allowlist.tsv
fail=0

check() {
  local pkg="$1" ver="$2"
  if ! awk -F'\t' -v p="$pkg" -v v="$ver" '$1==p && $2==v {found=1} END {exit !found}' "$ALLOWLIST"; then
    echo "LICENSE GATE: $pkg $ver is not on the allowlist ($ALLOWLIST)"
    fail=1
  fi
}

while read -r pkg ver; do
  check "$pkg" "$ver"
done < <(grep -rho 'PackageReference Include="[^"]*" Version="[^"]*"' --include='*.csproj' core tests rig \
  | sed -E 's/PackageReference Include="([^"]*)" Version="([^"]*)"/\1 \2/' | sort -u)

while read -r pkg ver; do
  check "$pkg" "$ver"
done < <(python3 -c "
import json
d = json.load(open('.config/dotnet-tools.json'))
for name, tool in d.get('tools', {}).items():
    print(name, tool['version'])
")

if [ "$fail" -eq 0 ]; then
  echo "license gate: clean — every dependency is allowlisted"
fi
exit $fail
