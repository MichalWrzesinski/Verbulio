#!/usr/bin/env bash
set -euo pipefail

if [[ $# != 6 ]]; then
  printf 'Usage: %s revision ci_run_id ci_attempt artifact_id artifact_sha256 ingress_sha256\n' "$0" >&2
  exit 2
fi
revision="$1" run_id="$2" attempt="$3" artifact_id="$4" artifact_digest="$5" ingress_digest="$6"

fail() {
  printf '%s\n' "$1" >&2
  exit 1
}

[[ "$revision" =~ ^[0-9a-f]{40}$ && "$run_id" =~ ^[1-9][0-9]*$ &&
   "$attempt" =~ ^[1-9][0-9]*$ && "$artifact_id" =~ ^[1-9][0-9]*$ &&
   "$artifact_digest" =~ ^[0-9a-f]{64}$ && "$ingress_digest" =~ ^[0-9a-f]{64}$ ]] || fail "Invalid candidate identifiers."
repo="${GITHUB_REPOSITORY:?Set GITHUB_REPOSITORY to the GitHub repository being verified}"
git merge-base --is-ancestor "$revision" HEAD || fail "Candidate is outside the checked-out main history."
observed_ingress="$(git show "$revision:scripts/public/nginx-location.conf" | sha256sum)"
[[ "${observed_ingress%% *}" == "$ingress_digest" ]] || fail "Candidate ingress hash does not match."

run="$(gh api "repos/$repo/actions/runs/$run_id/attempts/$attempt")"
jq -e --arg repo "$repo" --arg revision "$revision" --arg attempt "$attempt" '
  .path == ".github/workflows/ci.yml" and .event == "push" and .head_branch == "main"
    and .status == "completed" and .conclusion == "success" and .head_sha == $revision
    and .head_repository.full_name == $repo and (.run_attempt | tostring) == $attempt
' <<< "$run" >/dev/null || fail "The CI attempt did not successfully build this candidate on main."

name="verbulio-public-$attempt"
artifacts="$(gh api "repos/$repo/actions/runs/$run_id/artifacts?name=$name")"
jq -e --arg id "$artifact_id" --arg name "$name" --arg digest "sha256:$artifact_digest" '
  .artifacts | any((.id | tostring) == $id and .name == $name and .expired == false and .digest == $digest)
' <<< "$artifacts" >/dev/null || fail "The candidate artifact is missing or does not match the supplied digest."

fixture="$(mktemp -d)"
trap 'rm -rf -- "$fixture"' EXIT
gh api "repos/$repo/actions/artifacts/$artifact_id/zip" > "$fixture/artifact.zip"
observed_artifact="$(sha256sum "$fixture/artifact.zip")"
[[ "${observed_artifact%% *}" == "$artifact_digest" ]] || fail "Downloaded artifact digest does not match."
unzip -p "$fixture/artifact.zip" verbulio-public.tar.gz > "$fixture/verbulio-public.tar.gz"
gzip -t "$fixture/verbulio-public.tar.gz" || fail "Site archive is damaged."
archive_revision="$(tar -xOzf "$fixture/verbulio-public.tar.gz" ./REVISION)" || fail "Cannot read the site archive revision."
[[ "$archive_revision" == "$revision" ]] || fail "Archive revision does not match."
archive_hash="$(sha256sum "$fixture/verbulio-public.tar.gz")"

printf 'Revision: %s\nCI run: %s (attempt %s)\nArtifact ID: %s\nArtifact ZIP SHA-256: %s\nSite archive SHA-256: %s\nNginx ingress SHA-256: %s\n' \
  "$revision" "$run_id" "$attempt" "$artifact_id" "$artifact_digest" "${archive_hash%% *}" "$ingress_digest"
