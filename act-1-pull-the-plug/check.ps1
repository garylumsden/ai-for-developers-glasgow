#!/usr/bin/env pwsh
$project = Join-Path $PSScriptRoot "harness" "Act1Harness.csproj"
dotnet run --project $project --no-build -- check @args
exit $LASTEXITCODE
