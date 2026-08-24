#!/usr/bin/env bash

set -euo pipefail

version="${1:-0.1.0}"
build_number="${2:-1}"
architecture="${3:-arm64}"
output_directory="${4:-}"

if [[ "$(uname -s)" != 'Darwin' ]]; then
    printf 'macOS app bundles must be prepared on macOS.\n' >&2
    exit 1
fi

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    printf 'Version must contain three numeric parts, for example 0.1.0.\n' >&2
    exit 1
fi

if [[ ! "$build_number" =~ ^[1-9][0-9]*$ ]]; then
    printf 'Build number must be a positive integer.\n' >&2
    exit 1
fi

case "$architecture" in
    x64|arm64) ;;
    *)
        printf 'Architecture must be x64 or arm64.\n' >&2
        exit 1
        ;;
esac

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"
runtime_identifier="osx-$architecture"
project_path="$repository_root/src/ReqMint.App/ReqMint.App.csproj"
working_directory="$repository_root/artifacts/macos/$runtime_identifier"
publish_directory="$working_directory/publish"
bundle_path="$working_directory/ReqMint.app"
contents_directory="$bundle_path/Contents"
macos_directory="$contents_directory/MacOS"
resources_directory="$contents_directory/Resources"
plist_template="$repository_root/packaging/macos/Info.plist.in"
icon_generator="$repository_root/packaging/macos/GenerateAppIcon.swift"
entitlements_path="$repository_root/packaging/macos/ReqMint.entitlements"
signing_identity="${REQMINT_MACOS_SIGNING_IDENTITY:-}"

if [[ -z "$output_directory" ]]; then
    output_directory="$repository_root/artifacts/packages/macos"
fi

output_directory="$(mkdir -p -- "$output_directory" && cd -- "$output_directory" && pwd)"

case "$working_directory" in
    "$repository_root"/artifacts/macos/osx-x64|"$repository_root"/artifacts/macos/osx-arm64) ;;
    *)
        printf 'Refusing to clean an unexpected packaging directory: %s\n' "$working_directory" >&2
        exit 1
        ;;
esac

rm -rf -- "$working_directory"
mkdir -p -- "$macos_directory" "$resources_directory"

dotnet publish "$project_path" \
    --configuration Release \
    --runtime "$runtime_identifier" \
    --self-contained true \
    --output "$publish_directory" \
    -p:Version="$version" \
    -p:PublishSingleFile=false \
    -p:DebugSymbols=false \
    -p:DebugType=None \
    -p:UseAppHost=true

if [[ ! -f "$publish_directory/ReqMint.App" ]]; then
    printf 'The macOS executable was not created: %s\n' "$publish_directory/ReqMint.App" >&2
    exit 1
fi

if find "$publish_directory" -type f -name '*.pdb' -print -quit | grep -q .; then
    printf 'macOS release packages must not contain PDB files.\n' >&2
    exit 1
fi

cp -a -- "$publish_directory/." "$macos_directory/"
chmod 755 "$macos_directory/ReqMint.App"

sed \
    -e "s/{{VERSION}}/$version/g" \
    -e "s/{{BUILD_NUMBER}}/$build_number/g" \
    "$plist_template" > "$contents_directory/Info.plist"
plutil -lint "$contents_directory/Info.plist"

base_icon="$working_directory/ReqMint-1024.png"
iconset_directory="$working_directory/ReqMint.iconset"
swift "$icon_generator" "$base_icon"
mkdir -p -- "$iconset_directory"

create_icon() {
    local pixels="$1"
    local name="$2"
    sips -z "$pixels" "$pixels" "$base_icon" --out "$iconset_directory/$name" >/dev/null
}

create_icon 16 icon_16x16.png
create_icon 32 icon_16x16@2x.png
create_icon 32 icon_32x32.png
create_icon 64 icon_32x32@2x.png
create_icon 128 icon_128x128.png
create_icon 256 icon_128x128@2x.png
create_icon 256 icon_256x256.png
create_icon 512 icon_256x256@2x.png
create_icon 512 icon_512x512.png
create_icon 1024 icon_512x512@2x.png
iconutil --convert icns --output "$resources_directory/ReqMint.icns" "$iconset_directory"

if [[ -z "$signing_identity" ]]; then
    codesign_arguments=(--force --options runtime --sign - --entitlements "$entitlements_path")
else
    codesign_arguments=(--force --options runtime --timestamp --sign "$signing_identity" --entitlements "$entitlements_path")
fi

while IFS= read -r -d '' signable_file; do
    codesign "${codesign_arguments[@]}" "$signable_file"
done < <(find "$macos_directory" -type f -print0)

codesign "${codesign_arguments[@]}" "$bundle_path"
codesign --verify --deep --strict --verbose=2 "$bundle_path"

archive_name="ReqMint-$version-$build_number-$runtime_identifier.zip"
archive_path="$output_directory/$archive_name"
rm -f -- "$archive_path" "$archive_path.sha256"
ditto -c -k --sequesterRsrc --keepParent "$bundle_path" "$archive_path"

(
    cd -- "$output_directory"
    shasum -a 256 "$archive_name" > "$archive_name.sha256"
)

if [[ -z "$signing_identity" ]]; then
    printf 'Created ad-hoc signed ReqMint macOS test package: %s\n' "$archive_path"
else
    printf 'Created Developer ID-signed ReqMint macOS package: %s\n' "$archive_path"
fi
printf 'Created SHA-256 checksum: %s.sha256\n' "$archive_path"
if [[ -z "$signing_identity" ]]; then
    printf 'Developer ID signing and Apple notarization are still required for public distribution.\n'
fi
