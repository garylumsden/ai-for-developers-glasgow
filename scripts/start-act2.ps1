#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [ValidateRange(15, 300)]
    [int]$StartupTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repoRoot "act-2-council" "src" "GovernanceCouncil.slnx"
$toolsProject = Join-Path $repoRoot "act-2-council" "src" "CouncilTools.Mcp" "CouncilTools.Mcp.csproj"
$webProject = Join-Path $repoRoot "act-2-council" "src" "GovernanceCouncil.Web" "GovernanceCouncil.Web.csproj"
$envFile = Join-Path $repoRoot "act-2-council" "src" "GovernanceCouncil.Web" ".env"
$toolsHealth = "http://127.0.0.1:5199/health"
$webUrl = "http://127.0.0.1:5085/"
$startedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

if (-not (Test-Path $envFile)) {
    throw "Missing $envFile. Run azd provision from act-2-council/ first."
}

function Test-Endpoint([string]$Uri) {
    try {
        $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 3
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Start-DotnetProject {
    param(
        [string]$Project,
        [hashtable]$Environment
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new("dotnet")
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.ArgumentList.Add("run")
    $startInfo.ArgumentList.Add("--project")
    $startInfo.ArgumentList.Add($Project)
    $startInfo.ArgumentList.Add("-c")
    $startInfo.ArgumentList.Add("Release")
    $startInfo.ArgumentList.Add("--no-build")
    $startInfo.ArgumentList.Add("--no-launch-profile")
    foreach ($entry in $Environment.GetEnumerator()) {
        $startInfo.Environment[$entry.Key] = $entry.Value
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Could not start $Project."
    }
    $startedProcesses.Add($process)
    return $process
}

function Wait-ForEndpoint {
    param(
        [string]$Uri,
        [string]$Name,
        [System.Diagnostics.Process]$Process
    )

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $StartupTimeoutSeconds) {
        if ($null -ne $Process -and $Process.HasExited) {
            throw "$Name exited during startup with code $($Process.ExitCode)."
        }
        if (Test-Endpoint $Uri) {
            Write-Host "$Name ready: $Uri" -ForegroundColor Green
            return
        }
        Start-Sleep -Seconds 1
    }
    throw "$Name did not become ready within $StartupTimeoutSeconds seconds."
}

if (-not $SkipBuild) {
    Write-Host "Building Act 2..." -ForegroundColor Cyan
    dotnet build $solution -c Release -v minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

try {
    $toolsProcess = $null
    if (Test-Endpoint $toolsHealth) {
        Write-Host "council-tools is already running." -ForegroundColor Yellow
    }
    else {
        Write-Host "Starting council-tools..." -ForegroundColor Cyan
        $toolsProcess = Start-DotnetProject $toolsProject @{
            ASPNETCORE_URLS = "http://127.0.0.1:5199"
        }
        Wait-ForEndpoint $toolsHealth "council-tools" $toolsProcess
    }

    $webProcess = $null
    if (Test-Endpoint $webUrl) {
        Write-Host "The council web app is already running." -ForegroundColor Yellow
    }
    else {
        Write-Host "Starting The Glasgow Developer Council on Local MAF..." -ForegroundColor Cyan
        $webProcess = Start-DotnetProject $webProject @{
            ASPNETCORE_URLS = "http://127.0.0.1:5085"
            COUNCIL_AGENT_RUNTIME = "maf"
            COUNCIL_MODEL_PROFILE = "Fast"
        }
        Wait-ForEndpoint $webUrl "Council web app" $webProcess
    }

    Write-Host ""
    Write-Host "Act 2 is ready." -ForegroundColor Green
    Write-Host "Open: $webUrl"
    Write-Host "Tools health: $toolsHealth"
    Write-Host "Press Ctrl+C to stop processes started by this script."

    if ($startedProcesses.Count -eq 0) {
        return
    }

    while ($true) {
        foreach ($process in $startedProcesses) {
            if ($process.HasExited) {
                throw "A required Act 2 process exited with code $($process.ExitCode)."
            }
        }
        Start-Sleep -Seconds 1
    }
}
finally {
    foreach ($process in $startedProcesses) {
        if (-not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit()
        }
    }
}
