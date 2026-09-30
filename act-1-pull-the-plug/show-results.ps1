#!/usr/bin/env pwsh
$project = Join-Path $PSScriptRoot "harness" "Act1Harness.csproj"
dotnet run --project $project -- show-results @args
exit $LASTEXITCODE
