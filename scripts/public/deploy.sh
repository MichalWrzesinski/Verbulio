#!/usr/bin/env bash
set -euo pipefail

[[ $# == 2 && "$2" =~ ^[0-9a-f]{40}$ ]]
archive="$(realpath "$1")"
revision="$2"
source_dir="$(dirname "$archive")"
root=/srv/Verbulio/.runtime
umask 077
mkdir -p "$root/releases"
cd "$root"
exec 9>.deploy.lock
flock -n 9

(cd "$source_dir" && sha256sum --strict -c SHA256SUMS)
[[ "$(tar -xOzf "$archive" ./REVISION)" == "$revision" ]]
digest="$(sha256sum "$archive")"
release="$root/releases/$revision-${digest%% *}"
if [[ ! -d "$release" ]]; then
  candidate="$(mktemp -d releases/.extract.XXXXXX)"
  trap 'rm -rf -- "$candidate"' EXIT
  tar --extract --gzip --no-same-owner --file "$archive" --directory "$candidate"
  mv -T -- "$candidate" "$release"
  trap - EXIT
fi
test -f "$release/wwwroot/index.html"

container=verbulio-public-site
engine=(docker --host unix:///var/run/docker.sock)

start() {
  local selected="$1"
  "${engine[@]}" rm -f "$container" >/dev/null 2>&1 || true
  "${engine[@]}" run -d --name "$container" --network host --restart unless-stopped \
    --user "$(id -u):$(id -g)" --read-only --tmpfs /tmp --cap-drop ALL \
    --security-opt no-new-privileges --memory 256m \
    --mount "type=bind,source=$selected,target=/app,readonly" \
    -e ASPNETCORE_ENVIRONMENT=Public -w /app mcr.microsoft.com/dotnet/aspnet:10.0 \
    dotnet Verbulio.Api.dll >/dev/null
}

healthy() {
  local attempt
  for attempt in {1..20}; do
    if curl --noproxy '*' --fail --silent --max-time 2 http://127.0.0.1:5444/health \
      -H 'Host: verbulio.gitpush.app' -H 'X-Forwarded-For: 127.0.0.1' -H 'X-Forwarded-Proto: https' >/dev/null; then
      return 0
    fi
    sleep 1
  done
  return 1
}

previous=
if [[ -f current-release ]]; then
  previous="$(cat current-release)"
  [[ "$previous" =~ ^/srv/Verbulio/\.runtime/releases/[0-9a-f]{40}-[0-9a-f]{64}$ && -d "$previous" ]]
fi
if start "$release" && healthy; then
  install -m 644 "$source_dir/nginx-location.conf" nginx-location.conf.tmp
  mv -T nginx-location.conf.tmp nginx-location.conf
  printf '%s\n' "$release" > current-release.tmp
  mv -T current-release.tmp current-release
  printf 'Public release %s is healthy on loopback.\n' "$revision"
  exit 0
fi
printf 'Public candidate failed its health check.\n' >&2
if [[ -n "$previous" ]] && start "$previous" && healthy; then
  printf 'Restored the preceding public release.\n' >&2
else
  "${engine[@]}" rm -f "$container" >/dev/null 2>&1 || true
fi
exit 1
