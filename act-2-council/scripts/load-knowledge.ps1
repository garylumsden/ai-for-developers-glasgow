#!/usr/bin/env pwsh
param([switch]$Prune)

$ErrorActionPreference = "Stop"
$councilRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $councilRoot "src" "GovernanceCouncil.Web" ".env"
$source = Join-Path $councilRoot "data" "knowledge"
$container = "council-knowledge"

if (-not (Test-Path $envFile)) {
    throw "Missing $envFile. Run azd provision first."
}

$accountLine = Get-Content $envFile | Where-Object { $_ -match '^STORAGE_ACCOUNT_NAME=' } | Select-Object -First 1
$account = ($accountLine -split '=', 2)[1].Trim()
if (-not $account) {
    throw "STORAGE_ACCOUNT_NAME is missing from $envFile."
}

az storage container create --account-name $account --name $container --auth-mode login --output none
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$localNames = Get-ChildItem $source -Filter "*.md" | Select-Object -ExpandProperty Name
$remoteNames = @(az storage blob list --account-name $account --container-name $container --auth-mode login --query "[].name" -o tsv)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$stale = @($remoteNames | Where-Object { $_ -and $_ -notin $localNames })
if ($stale.Count -gt 0) {
    Write-Host "Remote files not present in the local knowledge pack: $($stale -join ', ')" -ForegroundColor Yellow
    if ($Prune) {
        $answer = Read-Host "Delete only these files from $container? Type YES"
        if ($answer -ne "YES") { throw "Prune cancelled." }
        foreach ($name in $stale) {
            az storage blob delete --account-name $account --container-name $container --name $name --auth-mode login --output none
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
    }
}

az storage blob upload-batch --account-name $account --destination $container --source $source `
    --pattern "*.md" --overwrite true --auth-mode login --output none
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Knowledge pack uploaded. Restart the council to refresh Foundry IQ ingestion." -ForegroundColor Green
