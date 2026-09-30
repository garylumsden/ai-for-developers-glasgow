#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
council_root="$(cd "$script_dir/.." && pwd)"
env_file="$council_root/src/GovernanceCouncil.Web/.env"
source_dir="$council_root/data/knowledge"
container="council-knowledge"
prune="${1:-}"

if [[ ! -f "$env_file" ]]; then
  echo "Missing $env_file. Run azd provision first." >&2
  exit 1
fi

account="$(sed -n 's/^STORAGE_ACCOUNT_NAME=//p' "$env_file" | head -n 1)"
if [[ -z "$account" ]]; then
  echo "STORAGE_ACCOUNT_NAME is missing from $env_file." >&2
  exit 1
fi

az storage container create --account-name "$account" --name "$container" --auth-mode login --output none

mapfile -t local_names < <(find "$source_dir" -maxdepth 1 -type f -name '*.md' -printf '%f\n' | sort)
mapfile -t remote_names < <(az storage blob list --account-name "$account" --container-name "$container" \
  --auth-mode login --query '[].name' -o tsv | sort)

stale=()
for remote in "${remote_names[@]}"; do
  [[ -z "$remote" ]] && continue
  if ! printf '%s\n' "${local_names[@]}" | grep -Fxq "$remote"; then
    stale+=("$remote")
  fi
done

if (( ${#stale[@]} > 0 )); then
  echo "Remote files not present in the local knowledge pack: ${stale[*]}"
  if [[ "$prune" == "--prune" ]]; then
    read -r -p "Delete only these files from $container? Type YES: " answer
    [[ "$answer" == "YES" ]] || { echo "Prune cancelled." >&2; exit 1; }
    for name in "${stale[@]}"; do
      az storage blob delete --account-name "$account" --container-name "$container" \
        --name "$name" --auth-mode login --output none
    done
  fi
fi

az storage blob upload-batch --account-name "$account" --destination "$container" \
  --source "$source_dir" --pattern '*.md' --overwrite true --auth-mode login --output none
echo "Knowledge pack uploaded. Restart the council to refresh Foundry IQ ingestion."
