#!/usr/bin/env pwsh
$project = Join-Path $PSScriptRoot ".." ".." "act-1-pull-the-plug" "harness" "Act1Harness.csproj"
dotnet run --project $project -- bridge @args
exit $LASTEXITCODE
