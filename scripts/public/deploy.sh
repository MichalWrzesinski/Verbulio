#!/usr/bin/env bash
set -euo pipefail

[[ $# == 3 && "$2" =~ ^[0-9a-f]{40}$ && "$3" =~ ^[0-9a-f]{64}$ ]] || exit 2
archive="$(realpath "$1")"
revision="$2"
source_dir="$(dirname "$archive")"
root=/srv/Verbulio/.runtime
umask 077
mkdir -p "$root/releases"
cd "$root"
exec 9>.deploy.lock
flock -n 9
ingress="$(sha256sum /etc/nginx/snippets/verbulio-public.conf)"
if [[ "${ingress%% *}" != "$3" ]] || ! cmp -s /etc/nginx/snippets/verbulio-public.conf "$source_dir/nginx-location.conf"; then
  printf 'Live Nginx ingress does not match the approved configuration.\n' >&2
  exit 1
fi

runtime_image=mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21

(cd "$source_dir" && sha256sum --strict -c SHA256SUMS)
archive_revision="$(tar -xOzf "$archive" ./REVISION)"
[[ "$archive_revision" == "$revision" ]] || exit 1
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
  local url="$1" expected_revision="$2" headers attempt
  shift 2
  for attempt in {1..20}; do
    if headers="$(curl --noproxy '*' --fail --silent --max-time 3 --dump-header - \
      --output /dev/null "$url" "$@")" &&
      grep -Eq '^HTTP/[^ ]+ 200[[:space:]]' <<< "$headers" &&
      { [[ -z "$expected_revision" ]] || printf '%s\n' "$headers" |
        tr -d '\r' | grep -iqx "X-Verbulio-Revision: $expected_revision"; }; then
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
if start "$release" && healthy https://verbulio.gitpush.app/health "$revision" && mv -T -- "$pointer/current-release" current-release; then
  printf 'Public release %s is healthy through public HTTPS.\n' "$revision"
  exit 0
fi
printf 'Public candidate failed verification or promotion.\n' >&2
if [[ -n "$previous" ]] && start "$previous"; then
  previous_revision="${previous##*/}"
  previous_revision="${previous_revision%%-*}"
  # The first public release predates the revision response header.
  if [[ "$previous_revision" == 006104217683613de617ecad481825860eb2d917 ]]; then
    previous_revision=
  fi
  if healthy http://127.0.0.1:5444/health "$previous_revision" \
    -H 'Host: verbulio.gitpush.app' -H 'X-Forwarded-For: 127.0.0.1' -H 'X-Forwarded-Proto: https'; then
    printf 'Restored the preceding public release on loopback.\n' >&2
    if healthy https://verbulio.gitpush.app/health "$previous_revision"; then
      printf 'Public HTTPS also recovered.\n' >&2
    else
      printf 'Public HTTPS remains unavailable. The restored runtime is still running.\n' >&2
    fi
    exit 1
  fi
fi
"${engine[@]}" rm -f "$container" >/dev/null 2>&1 || true
exit 1
