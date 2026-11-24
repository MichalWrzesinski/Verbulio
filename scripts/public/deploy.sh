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
if [[ -e /etc/nginx/snippets/verbulio-public.conf ]]; then
  printf 'The staging workflow cannot replace a publicly activated release.\n' >&2
  exit 1
fi

runtime_image=mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21

(cd "$source_dir" && sha256sum --strict -c SHA256SUMS)
[[ "$(tar -xOzf "$archive" ./REVISION)" == "$revision" ]]
digest="$(sha256sum "$archive")"
release="$root/releases/$revision-${digest%% *}"
if [[ ! -d "$release" ]]; then
  candidate="$(mktemp -d releases/.extract.XXXXXX)"
  trap 'rm -rf -- "$candidate"' EXIT
  tar --extract --gzip --no-same-owner --file "$archive" --directory "$candidate"
  install -m 644 "$source_dir/nginx-location.conf" "$candidate/nginx-location.conf"
  printf '%s\n' "$runtime_image" > "$candidate/runtime-image"
  mv -T -- "$candidate" "$release"
  trap - EXIT
fi
test -f "$release/wwwroot/index.html"

container=verbulio-public-site
engine=(docker --host unix:///var/run/docker.sock)

start() {
  local selected="$1"
  local image
  image="$(cat "$selected/runtime-image")" || return 1
  [[ "$image" =~ ^mcr\.microsoft\.com/dotnet/aspnet:10\.0@sha256:[0-9a-f]{64}$ ]] || return 1
  if ! "${engine[@]}" image inspect "$image" >/dev/null 2>&1; then
    "${engine[@]}" pull "$image" || return 1
  fi
  "${engine[@]}" rm -f "$container" >/dev/null 2>&1 || true
  "${engine[@]}" run -d --name "$container" --network host --restart unless-stopped \
    --user "$(id -u):$(id -g)" --read-only --tmpfs /tmp --cap-drop ALL \
    --security-opt no-new-privileges --memory 256m \
    --mount "type=bind,source=$selected,target=/app,readonly" \
    -e ASPNETCORE_ENVIRONMENT=Public -w /app --pull never "$image" \
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
if [[ -L current-release ]]; then
  previous="$(readlink current-release)"
  [[ "$previous" =~ ^/srv/Verbulio/\.runtime/releases/[0-9a-f]{40}-[0-9a-f]{64}$ && -d "$previous" ]]
fi
pointer="$(mktemp -d .promote.XXXXXX)"
trap 'rm -rf -- "$pointer"' EXIT
ln -s -- "$release" "$pointer/current-release"
if start "$release" && healthy && mv -T -- "$pointer/current-release" current-release; then
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
