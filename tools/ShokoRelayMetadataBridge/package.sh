#!/usr/bin/env bash
set -Eeuo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
output="$(realpath -m -- "${1:?output directory}")"
mkdir -p "$output"
project="$root/tools/ShokoRelayMetadataBridge/ShokoRelayMetadataBridge.csproj"
version="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$project")"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
dotnet test "$root/tools/ShokoRelayMetadataBridge.Tests/ShokoRelayMetadataBridge.Tests.csproj" -c Release
dotnet publish "$project" -c Release -o "$output/publish"
cp "$output/publish/ShokoRelayMetadataBridge.dll" "$output/"
(cd "$output" && zip -j "ShokoRelayMetadataBridge-$version.zip" ShokoRelayMetadataBridge.dll)
checksum="$(sha256sum "$output/ShokoRelayMetadataBridge-$version.zip" | cut -d' ' -f1)"
commit="$(git -C "$root" rev-parse HEAD)"
tag="metadata-bridge-v$version"
release="$(jq -n --arg version "$version" --arg tag "$tag" --arg commit "$commit" \
  --arg at "$(date -u +%FT%TZ)" --arg sha "$checksum" \
  --arg url "https://github.com/JosephZurita/AniSync/releases/download/$tag/ShokoRelayMetadataBridge-$version.zip" '
  {version:$version,tag:$tag,source_revision:$commit,released_at:$at,channel:"Stable",
   release_notes:"Tested metadata-event bridge for the coordinated Shoko/Relay stack.",
   archives:[{runtime:"any",abstraction:"6.0.0",url:$url,checksum:$sha}]}')"
existing='[]'
if [[ -f "$output/existing-manifest.json" ]]; then
  existing="$(jq -ce '.releases' "$output/existing-manifest.json")"
fi
jq -n --argjson release "$release" --argjson existing "$existing" '
  {type:"package",id:"83afc08d-34e3-4af5-8851-4528ec19cb39",name:"Shoko Relay Metadata Bridge",
   overview:"Refreshes Plex through Relay when Shoko receives updated metadata.",authors:"JosephZurita",
   repository_url:"https://github.com/JosephZurita/AniSync",tags:["plex","metadata"],
   releases:([$release] + [$existing[] | select(.version != $release.version)])}' >"$output/manifest.json"
(cd "$output" && sha256sum "ShokoRelayMetadataBridge-$version.zip" >"ShokoRelayMetadataBridge-$version.zip.sha256")
[[ "$(unzip -Z1 "$output/ShokoRelayMetadataBridge-$version.zip")" == ShokoRelayMetadataBridge.dll ]]
jq -e '.id == "83afc08d-34e3-4af5-8851-4528ec19cb39" and .releases[0].channel == "Stable" and
  (.releases[0].archives[0].checksum | test("^[0-9a-f]{64}$"))' "$output/manifest.json" >/dev/null
