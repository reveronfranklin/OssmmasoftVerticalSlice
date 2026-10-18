#!/usr/bin/env bash
set -euo pipefail
component=${1:?Falta componente}
artifact=${2:?Falta artefacto}
[[ "$component" =~ ^(legacy|vertical|frontend)$ ]] || exit 2
[[ "${GITHUB_REF:-}" == refs/heads/develop ]] || exit 2
: "${QA_SSH_HOST:?}" "${QA_SSH_USER:?}" "${QA_SSH_PRIVATE_KEY:?}" "${QA_SSH_KNOWN_HOSTS:?}"
[[ "$QA_SSH_HOST" =~ ^[a-zA-Z0-9.-]+$ ]] || exit 2
[[ "$QA_SSH_USER" =~ ^[a-zA-Z0-9_-]+$ ]] || exit 2
version="${GITHUB_SHA}-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}"
[[ "$version" =~ ^[0-9a-f]{40}-[0-9]+-[0-9]+$ ]] || exit 2
[[ $(stat -c %s "$artifact") -le 1073741824 ]] || { echo 'Artefacto mayor a 1 GiB.' >&2; exit 2; }
ssh_dir=$(mktemp -d "$RUNNER_TEMP/qa-ssh.XXXXXXXX")
trap 'rm -rf "$ssh_dir"' EXIT
chmod 700 "$ssh_dir"
printf '%s\n' "$QA_SSH_PRIVATE_KEY" > "$ssh_dir/key"
printf '%s\n' "$QA_SSH_KNOWN_HOSTS" > "$ssh_dir/known_hosts"
chmod 600 "$ssh_dir/key" "$ssh_dir/known_hosts"
ssh -T -i "$ssh_dir/key" -o BatchMode=yes -o IdentitiesOnly=yes \
  -o StrictHostKeyChecking=yes -o UserKnownHostsFile="$ssh_dir/known_hosts" \
  -o ConnectTimeout=20 -o ServerAliveInterval=30 -o ServerAliveCountMax=6 \
  "$QA_SSH_USER@$QA_SSH_HOST" "deploy-qa $component $version" < "$artifact"
printf 'Publicado %s: %s\n' "$component" "$version" >> "$GITHUB_STEP_SUMMARY"
