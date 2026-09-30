#!/usr/bin/env pwsh
param([switch]$WhatIf)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$act1 = Join-Path $repoRoot "act-1-pull-the-plug"
$council = Join-Path $repoRoot "act-2-council"
$act3 = Join-Path $repoRoot "act-3-audience-council"

$targets = @(
    "Back up and clear Act 1 live results.",
    "Recreate Act 1 local and cloud workspaces.",
    "Delete only rehearsal Assessments, Nexuses, and deliberations. Preserve containers and indexes.",
    "Clear Act 1 evidence between the two dossier markers.",
    "Refresh council-tools snapshots from public APIs when available.",
    "List retained Act 3 rehearsal repositories. Do not delete them."
)

Write-Host "Reset targets:" -ForegroundColor Cyan
$targets | ForEach-Object { Write-Host "  - $_" }
if ($WhatIf) {
    Write-Host "`nWhat-if only. No data changed." -ForegroundColor Yellow
    exit 0
}

$answer = Read-Host "`nType RESET to apply the listed changes"
if ($answer -ne "RESET") {
    throw "Reset cancelled."
}

$audit = [System.Collections.Generic.List[string]]::new()
$results = Join-Path $act1 "results" "results.json"
if (Test-Path $results) {
    $backupDirectory = Join-Path $act1 "results" "backups"
    New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
    $backup = Join-Path $backupDirectory "results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    Copy-Item $results $backup
    Remove-Item $results
    $audit.Add("Act 1 results backed up to $backup and cleared.")
}
else {
    $audit.Add("Act 1 live results were already absent.")
}

& (Join-Path $act1 "prepare.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$audit.Add("Act 1 workspaces recreated.")

dotnet run --project (Join-Path $council "src" "CouncilMaintenance" "CouncilMaintenance.csproj") -- --yes
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$audit.Add("Only configured rehearsal Assessments, Nexuses, and deliberations were deleted.")

dotnet run --project (Join-Path $act1 "harness" "Act1Harness.csproj") -- clear-dossier
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$audit.Add("Act 1 dossier markers cleared.")

dotnet run --project (Join-Path $council "src" "CouncilTools.Mcp" "CouncilTools.Mcp.csproj") -- --self-test
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$audit.Add("Council tool snapshots refreshed or retained through cached fallback.")

$rehearsalPath = Join-Path $act3 "rehearsal-repositories.json"
$rehearsalRepos = @(Get-Content $rehearsalPath -Raw | ConvertFrom-Json)
Write-Host "`nRetained rehearsal repositories:" -ForegroundColor Cyan
foreach ($repo in $rehearsalRepos) {
    Write-Host "  - $($repo.url)"
    Write-Host "    Optional manual cleanup: gh repo delete $($repo.owner)/$($repo.name)"
}
$audit.Add("$($rehearsalRepos.Count) Act 3 rehearsal repository or repositories listed. None deleted.")

Write-Host "`nReset audit summary:" -ForegroundColor Green
$audit | ForEach-Object { Write-Host "  - $_" }
Write-Host "Reload open debate pages to clear view-only chat, hands, and typing state."
