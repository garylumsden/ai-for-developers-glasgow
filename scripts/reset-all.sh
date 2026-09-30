#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
act1="$repo_root/act-1-pull-the-plug"
council="$repo_root/act-2-council"
act3="$repo_root/act-3-audience-council"

cat <<'EOF'
Reset targets:
  - Back up and clear Act 1 live results.
  - Recreate Act 1 local and cloud workspaces.
  - Delete only rehearsal Assessments, Nexuses, and deliberations. Preserve containers and indexes.
  - Clear Act 1 evidence between the two dossier markers.
  - Refresh council-tools snapshots from public APIs when available.
  - List retained Act 3 rehearsal repositories. Do not delete them.
EOF

if [[ "${1:-}" == "--what-if" ]]; then
  echo
  echo "What-if only. No data changed."
  exit 0
fi

read -r -p "Type RESET to apply the listed changes: " answer
[[ "$answer" == "RESET" ]] || { echo "Reset cancelled." >&2; exit 1; }

audit=()
results="$act1/results/results.json"
if [[ -f "$results" ]]; then
  backup_dir="$act1/results/backups"
  mkdir -p "$backup_dir"
  backup="$backup_dir/results-$(date '+%Y%m%d-%H%M%S').json"
  cp "$results" "$backup"
  rm "$results"
  audit+=("Act 1 results backed up to $backup and cleared.")
else
  audit+=("Act 1 live results were already absent.")
fi

"$act1/prepare.sh"
audit+=("Act 1 workspaces recreated.")

dotnet run --project "$council/src/CouncilMaintenance/CouncilMaintenance.csproj" -- --yes
audit+=("Only configured rehearsal Assessments, Nexuses, and deliberations were deleted.")

dotnet run --project "$act1/harness/Act1Harness.csproj" -- clear-dossier
audit+=("Act 1 dossier markers cleared.")

dotnet run --project "$council/src/CouncilTools.Mcp/CouncilTools.Mcp.csproj" -- --self-test
audit+=("Council tool snapshots refreshed or retained through cached fallback.")

echo
echo "Retained rehearsal repositories:"
cat "$act3/rehearsal-repositories.json"
echo "No repository was deleted. Use gh repo delete manually after review."
audit+=("Act 3 rehearsal repositories listed. None deleted.")

echo
echo "Reset audit summary:"
printf '  - %s\n' "${audit[@]}"
echo "Reload open debate pages to clear view-only chat, hands, and typing state."
