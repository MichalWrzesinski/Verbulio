#!/usr/bin/env bash
set -euo pipefail

if [[ $# != 1 || "$1" != /* || "$1" != *.tar.gz ]]; then
  printf 'Usage: %s /absolute/path/verbulio-preview.tar.gz\n' "$0" >&2
  exit 2
fi

archive="$1"
if [[ -e "$archive" ]]; then
  printf 'Output already exists: %s\n' "$archive" >&2
  exit 2
fi

repo="$(cd "$(dirname "$0")/.." && pwd)"
if ! git -C "$repo" diff --quiet HEAD --; then
  printf 'Build the preview from a clean tracked checkout.\n' >&2
  exit 2
fi
package="$(mktemp -d)"
trap 'rm -rf -- "$package"' EXIT

(
  cd "$repo/client"
  npm run build -- --configuration production,preview --output-path "$package/client"
)
(
  cd "$repo/api"
  dotnet publish Verbulio.Api -c Release --no-self-contained -p:UseAppHost=false -o "$package/app"
)
mkdir -p "$package/app/wwwroot"
cp -R "$package/client/browser/." "$package/app/wwwroot/"
git -C "$repo" rev-parse HEAD > "$package/app/REVISION"
tar -czf "$package/preview.tar.gz" -C "$package/app" .
cp "$package/preview.tar.gz" "$archive"
