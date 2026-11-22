#!/usr/bin/env bash
set -euo pipefail

[[ $# == 2 && "$2" =~ ^[0-9a-f]{40}$ ]]
archive="$(realpath "$1")"
revision="$2"
source_dir="$(dirname "$archive")"
cd /srv/Verbulio-preview
umask 077
exec 9>.deploy.lock
flock -n 9

(cd "$source_dir" && sha256sum --strict -c SHA256SUMS)
[[ "$(tar -xOzf "$archive" ./REVISION)" == "$revision" ]]
mkdir -p releases keys tls
archive_digest="$(sha256sum "$archive")"
compose_digest="$(sha256sum "$source_dir/compose.yaml")"
release="$(pwd)/releases/$revision-${archive_digest%% *}-${compose_digest%% *}"
if [[ ! -d "$release" ]]; then
  candidate="$(mktemp -d releases/.extract.XXXXXX)"
  trap 'rm -rf -- "$candidate"' EXIT
  tar --extract --gzip --no-same-owner --file "$archive" --directory "$candidate"
  cp "$source_dir/compose.yaml" "$candidate/.preview-compose.yaml"
  mv -T -- "$candidate" "$release"
  trap - EXIT
fi
if [[ ! -f .env ]]; then
  password="$(openssl rand -hex 32)"
  printf 'POSTGRES_PASSWORD=%s\n' "$password" > .env.tmp
  mv .env.tmp .env
  unset password
fi
if [[ ! -s tls/preview.key ]] || ! openssl x509 -in tls/preview.crt -noout -checkend 172800 >/dev/null 2>&1; then
  openssl req -x509 -newkey rsa:2048 -nodes -days 90 -subj '/CN=Verbulio private preview' \
    -addext 'subjectAltName=IP:127.0.0.1' -addext 'basicConstraints=critical,CA:FALSE' \
    -keyout tls/preview.key.tmp -out tls/preview.crt.tmp
  mv tls/preview.key.tmp tls/preview.key
  mv tls/preview.crt.tmp tls/preview.crt
  printf 'Preview certificate renewed. Refresh the trusted copy before viewing.\n'
fi
install -m 644 tls/preview.crt preview.crt
PREVIEW_UID="$(id -u)"
PREVIEW_GID="$(id -g)"
export PREVIEW_UID PREVIEW_GID
unset POSTGRES_PASSWORD

compose() {
  local selected="$1"
  shift
  PREVIEW_RELEASE="$selected" docker --host unix:///var/run/docker.sock compose --project-name verbulio-preview \
    --project-directory "$PWD" --env-file "$PWD/.env" -f "$selected/.preview-compose.yaml" "$@"
}

healthy() {
  local attempt
  for attempt in {1..30}; do
    if curl --noproxy '*' --cacert preview.crt --connect-timeout 2 --max-time 3 \
      --fail --silent https://127.0.0.1:5443/health > /dev/null; then
      return 0
    fi
    if [[ "$attempt" -lt 30 ]]; then sleep 1; fi
  done
  return 1
}

compose "$release" config --quiet
compose "$release" up -d --wait db
compose "$release" run --rm --no-deps app dotnet Verbulio.Api.dll --migrate
if compose "$release" up -d --no-deps --force-recreate app && healthy; then
  printf '%s\n' "$release" > current-release.tmp
  mv current-release.tmp current-release
  printf 'Preview release %s is healthy.\n' "$revision"
  exit 0
fi

printf 'Candidate failed its health check.\n' >&2
if [[ -f current-release ]]; then
  previous="$(cat current-release)"
  if compose "$previous" up -d --no-deps --force-recreate app && healthy; then
    printf 'Restored the preceding healthy app release.\n' >&2
    exit 1
  fi
fi
compose "$release" stop app
if [[ -f current-release ]]; then
  mv current-release last-known-healthy
fi
exit 1
