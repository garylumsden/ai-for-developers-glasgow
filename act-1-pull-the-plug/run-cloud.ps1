#!/usr/bin/env pwsh
$project = Join-Path $PSScriptRoot "harness" "Act1Harness.csproj"
dotnet run --project $project --no-build -- run-cloud @args
exit $LASTEXITCODE
