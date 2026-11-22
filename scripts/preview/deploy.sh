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
release="$(pwd)/releases/$revision"
if [[ ! -d "$release" ]]; then
  candidate="$(mktemp -d releases/.extract.XXXXXX)"
  trap 'rm -rf -- "$candidate"' EXIT
  tar --extract --gzip --no-same-owner --file "$archive" --directory "$candidate"
  mv -T -- "$candidate" "$release"
  trap - EXIT
fi
if [[ ! -f .env ]]; then
  printf 'POSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 32)" > .env.tmp
  mv .env.tmp .env
fi
if [[ ! -s tls/preview.crt || ! -s tls/preview.key ]]; then
  openssl req -x509 -newkey rsa:2048 -nodes -days 90 -subj '/CN=Verbulio private preview' \
    -addext 'subjectAltName=IP:127.0.0.1' -addext 'basicConstraints=critical,CA:FALSE' \
    -keyout tls/preview.key -out tls/preview.crt
fi
install -m 644 tls/preview.crt preview.crt
PREVIEW_UID="$(id -u)"
PREVIEW_GID="$(id -g)"
PREVIEW_RELEASE="$release"
export PREVIEW_UID PREVIEW_GID PREVIEW_RELEASE
unset POSTGRES_PASSWORD
install -m 600 "$source_dir/compose.yaml" compose.yaml
compose=(docker --host unix:///var/run/docker.sock compose --project-name verbulio-preview
  --project-directory "$PWD" --env-file "$PWD/.env" -f "$PWD/compose.yaml")
"${compose[@]}" config --quiet
"${compose[@]}" up -d --wait db
"${compose[@]}" run --rm --no-deps app dotnet Verbulio.Api.dll --migrate
"${compose[@]}" up -d --no-deps app
for attempt in {1..30}; do
  if curl --noproxy '*' --cacert preview.crt --connect-timeout 2 --max-time 3 \
    --fail --silent https://127.0.0.1:5443/health > /dev/null; then
    printf '%s\n' "$revision" > current-revision.tmp
    mv current-revision.tmp current-revision
    printf 'Preview release %s is healthy.\n' "$revision"
    exit 0
  fi
  if [[ "$attempt" -lt 30 ]]; then sleep 1; fi
done
"${compose[@]}" ps
exit 1
