#!/usr/bin/env bash

set -euo pipefail

version="${1:-0.1.0-preview.1}"
architecture="${2:-x64}"
output_directory="${3:-}"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    printf 'Version must be a three-part semantic version, optionally followed by a prerelease label.\n' >&2
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
runtime_identifier="linux-$architecture"
project_path="$repository_root/src/ReqMint.App/ReqMint.App.csproj"
working_directory="$repository_root/artifacts/linux/$runtime_identifier"
publish_directory="$working_directory/publish"
package_directory="$working_directory/package/ReqMint"
launcher_path="$repository_root/packaging/linux/reqmint"
readme_path="$repository_root/packaging/linux/README.txt"

if [[ -z "$output_directory" ]]; then
    output_directory="$repository_root/artifacts/packages/linux"
fi

output_directory="$(mkdir -p -- "$output_directory" && cd -- "$output_directory" && pwd)"

case "$working_directory" in
    "$repository_root"/artifacts/linux/linux-x64|"$repository_root"/artifacts/linux/linux-arm64) ;;
    *)
        printf 'Refusing to clean an unexpected packaging directory: %s\n' "$working_directory" >&2
        exit 1
        ;;
esac

rm -rf -- "$working_directory"
mkdir -p -- "$package_directory"

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
    printf 'The Linux executable was not created: %s\n' "$publish_directory/ReqMint.App" >&2
    exit 1
fi

if find "$publish_directory" -type f -name '*.pdb' -print -quit | grep -q .; then
    printf 'Portable release packages must not contain PDB files.\n' >&2
    exit 1
fi

cp -a -- "$publish_directory/." "$package_directory/"
cp -- "$launcher_path" "$package_directory/reqmint"
cp -- "$readme_path" "$package_directory/README.txt"
cp -- "$repository_root/LICENSE" "$package_directory/LICENSE.txt"
printf '%s\n' "$version" > "$package_directory/VERSION"
chmod 755 "$package_directory/ReqMint.App" "$package_directory/reqmint"

archive_name="ReqMint-$version-$runtime_identifier.tar.gz"
archive_path="$output_directory/$archive_name"
rm -f -- "$archive_path" "$archive_path.sha256"

tar \
    --sort=name \
    --mtime='UTC 2020-01-01' \
    --owner=0 \
    --group=0 \
    --numeric-owner \
    -czf "$archive_path" \
    -C "$working_directory/package" \
    ReqMint

(
    cd -- "$output_directory"
    sha256sum "$archive_name" > "$archive_name.sha256"
)

printf 'Created ReqMint Linux portable package: %s\n' "$archive_path"
printf 'Created SHA-256 checksum: %s.sha256\n' "$archive_path"
