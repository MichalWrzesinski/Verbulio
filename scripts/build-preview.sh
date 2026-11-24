#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 || $# -gt 2 || "$1" != /* || "$1" != *.tar.gz ]]; then
  printf 'Usage: %s /absolute/path/verbulio-site.tar.gz [preview|public]\n' "$0" >&2
  exit 2
fi

archive="$1"
case "${2:-preview}" in
  preview) configuration=production,preview ;;
  public) configuration=production ;;
  *) exit 2 ;;
esac
if [[ -e "$archive" ]]; then
  printf 'Output already exists: %s\n' "$archive" >&2
  exit 2
fi

repo="$(cd "$(dirname "$0")/.." && pwd)"
if ! git -C "$repo" diff --quiet HEAD --; then
  printf 'Build the site from a clean tracked checkout.\n' >&2
  exit 2
fi
package="$(mktemp -d)"
trap 'rm -rf -- "$package"' EXIT

(
  cd "$repo/client"
  npm run build -- --configuration "$configuration" --output-path "$package/client"
)
(
  cd "$repo/api"
  dotnet publish Verbulio.Api -c Release --no-self-contained -p:UseAppHost=false -o "$package/app"
)
mkdir -p "$package/app/wwwroot"
cp -R "$package/client/browser/." "$package/app/wwwroot/"
git -C "$repo" rev-parse HEAD > "$package/app/REVISION"
tar -czf "$package/site.tar.gz" -C "$package/app" .
cp "$package/site.tar.gz" "$archive"
