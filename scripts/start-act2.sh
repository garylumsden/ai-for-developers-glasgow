#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
solution="$repo_root/act-2-council/src/GovernanceCouncil.slnx"
tools_project="$repo_root/act-2-council/src/CouncilTools.Mcp/CouncilTools.Mcp.csproj"
web_project="$repo_root/act-2-council/src/GovernanceCouncil.Web/GovernanceCouncil.Web.csproj"
env_file="$repo_root/act-2-council/src/GovernanceCouncil.Web/.env"
tools_health="http://127.0.0.1:5199/health"
web_url="http://127.0.0.1:5085/"
skip_build=false
tools_pid=""
web_pid=""

if [[ "${1:-}" == "--skip-build" ]]; then
  skip_build=true
fi

if [[ ! -f "$env_file" ]]; then
  echo "Missing $env_file. Run azd provision from act-2-council/ first." >&2
  exit 1
fi

cleanup() {
  for pid in "$web_pid" "$tools_pid"; do
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
      wait "$pid" 2>/dev/null || true
    fi
  done
}
trap cleanup EXIT INT TERM

wait_for_url() {
  local url="$1"
  local name="$2"
  local pid="$3"
  local attempts=0
  while (( attempts < 120 )); do
    if [[ -n "$pid" ]] && ! kill -0 "$pid" 2>/dev/null; then
      echo "$name exited during startup." >&2
      exit 1
    fi
    if curl --fail --silent "$url" >/dev/null 2>&1; then
      echo "$name ready: $url"
      return
    fi
    sleep 1
    ((attempts += 1))
  done
  echo "$name did not become ready within 120 seconds." >&2
  exit 1
}

if [[ "$skip_build" == false ]]; then
  echo "Building Act 2..."
  dotnet build "$solution" -c Release -v minimal
fi

if curl --fail --silent "$tools_health" >/dev/null 2>&1; then
  echo "council-tools is already running."
else
  echo "Starting council-tools..."
  ASPNETCORE_URLS="http://127.0.0.1:5199" \
    dotnet run --project "$tools_project" -c Release --no-build --no-launch-profile &
  tools_pid=$!
  wait_for_url "$tools_health" "council-tools" "$tools_pid"
fi

if curl --fail --silent "$web_url" >/dev/null 2>&1; then
  echo "The council web app is already running."
else
  echo "Starting The Glasgow Developer Council on Local MAF..."
  ASPNETCORE_URLS="http://127.0.0.1:5085" \
  COUNCIL_AGENT_RUNTIME="maf" \
  COUNCIL_MODEL_PROFILE="Fast" \
    dotnet run --project "$web_project" -c Release --no-build --no-launch-profile &
  web_pid=$!
  wait_for_url "$web_url" "Council web app" "$web_pid"
fi

echo
echo "Act 2 is ready."
echo "Open: $web_url"
echo "Tools health: $tools_health"

if [[ -z "$tools_pid" && -z "$web_pid" ]]; then
  exit 0
fi

echo "Press Ctrl+C to stop processes started by this script."
while true; do
  for pid in "$web_pid" "$tools_pid"; do
    if [[ -n "$pid" ]] && ! kill -0 "$pid" 2>/dev/null; then
      wait "$pid"
    fi
  done
  sleep 1
done
