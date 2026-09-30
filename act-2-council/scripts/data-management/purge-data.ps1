<#
.SYNOPSIS
    Deletes and recreates selected Governance Council data containers.
.DESCRIPTION
    The default remains a full demo-data purge. Callers can select Cosmos containers and skip Blob
    Storage. Every mutation uses PowerShell ShouldProcess.
.EXAMPLE
    .\purge-data.ps1
    .\purge-data.ps1 -WhatIf
    .\purge-data.ps1 -CosmosContainerNames assessments,nexuses,deliberations -SkipBlobStorage
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string]$CosmosContainerNames = "assessments,nexuses,dossiers,deliberations",
    [switch]$SkipBlobStorage
)

. $PSScriptRoot\..\gc-common.ps1

$ErrorActionPreference = "Stop"

$CosmosEndpoint = $script:CosmosEndpoint
$DatabaseName = $script:CosmosDatabaseName
$requestedContainers = $CosmosContainerNames.Split(',', [StringSplitOptions]::RemoveEmptyEntries) |
    ForEach-Object { $_.Trim() }
$CosmosContainers = @($script:CosmosContainers | Where-Object { $_.Name -in $requestedContainers })
if ($CosmosContainers.Count -ne $requestedContainers.Count) {
    throw "Unknown Cosmos container requested. Allowed: $($script:CosmosContainers.Name -join ', ')."
}
$StorageAccount = $script:StorageAccountName
$BlobContainers = $script:BlobContainers

$cosmosAccountName = ([Uri]$CosmosEndpoint).Host.Split('.')[0]
$resourceGroup = Get-RequiredSetting 'AZURE_RESOURCE_GROUP'

Write-Host "`n=== Cosmos DB ===" -ForegroundColor Cyan
Write-Host "Account: $cosmosAccountName | Database: $DatabaseName"

foreach ($c in $CosmosContainers) {
    if ($PSCmdlet.ShouldProcess("$DatabaseName/$($c.Name)", "Delete and recreate Cosmos container")) {
        $deleteOutput = & az cosmosdb sql container delete `
            --account-name $cosmosAccountName `
            --resource-group $resourceGroup `
            --database-name $DatabaseName `
            --name $c.Name `
            --yes 2>&1
        $deleteExitCode = $LASTEXITCODE
        if ($deleteExitCode -eq 0) {
            Write-Host "  Deleted: $($c.Name)" -ForegroundColor Red
        }
        elseif (($deleteOutput -join "`n") -match 'NotFound|ResourceNotFound') {
            Write-Host "  Not found (skip delete): $($c.Name)" -ForegroundColor Yellow
        }
        else {
            throw "Failed to delete Cosmos container '$($c.Name)': $($deleteOutput -join ' ')"
        }

        & az cosmosdb sql container create --account-name $cosmosAccountName --resource-group $resourceGroup `
            --database-name $DatabaseName --name $c.Name --partition-key-path $c.PartitionKey 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to recreate Cosmos container '$($c.Name)'."
        }
        Write-Host "  Created: $($c.Name) (partition: $($c.PartitionKey))" -ForegroundColor Green
    }
}

if (-not $SkipBlobStorage) {
    Write-Host "`n=== Blob Storage ===" -ForegroundColor Cyan
    Write-Host "Account: $StorageAccount"

    $storageToken = Get-StorageToken
    $storageBase = "https://$StorageAccount.blob.core.windows.net"

    foreach ($container in $BlobContainers) {
        $containerUrl = "$storageBase/$container`?restype=container"

        if ($PSCmdlet.ShouldProcess($container, "Delete and recreate Blob container")) {
            try {
                Invoke-WebRequest -Uri $containerUrl `
                    -Headers @{ "Authorization" = "******"; "x-ms-version" = "2021-08-06" } `
                    -Method Delete | Out-Null
                Write-Host "  Deleted: $container" -ForegroundColor Red
                Start-Sleep -Seconds 5
            }
            catch {
                if ($_.Exception.Response.StatusCode -eq 404) {
                    Write-Host "  Not found (skip delete): $container" -ForegroundColor Yellow
                }
                else { throw }
            }

            $retries = 0
            while ($retries -lt 6) {
                try {
                    Invoke-WebRequest -Uri $containerUrl `
                        -Headers @{ "Authorization" = "******"; "x-ms-version" = "2021-08-06" } `
                        -Method Put | Out-Null
                    Write-Host "  Created: $container" -ForegroundColor Green
                    break
                }
                catch {
                    if ($_.Exception.Response.StatusCode -eq 409 -and $retries -lt 5) {
                        $retries++
                        Write-Host "  Waiting for delete to propagate ($retries/5)..." -ForegroundColor DarkGray
                        Start-Sleep -Seconds 10
                    }
                    else { throw }
                }
            }
        }
    }
}

Write-Host "`nPurge complete." -ForegroundColor Green
