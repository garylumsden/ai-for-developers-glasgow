#!/usr/bin/env pwsh
$project = Join-Path $PSScriptRoot "harness" "Act1Harness.csproj"
dotnet run --project $project -- prepare @args
exit $LASTEXITCODE
