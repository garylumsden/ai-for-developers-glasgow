#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$script_dir/../../act-1-pull-the-plug/harness/Act1Harness.csproj"
dotnet run --project "$project" -- bridge "$@"
