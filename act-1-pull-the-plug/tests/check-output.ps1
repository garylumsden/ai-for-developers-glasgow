#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
$actRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $actRoot "harness" "Act1Harness.csproj"
$assembly = Join-Path $actRoot "harness" "bin" "Release" "net10.0" "Act1Harness.dll"
$workspace = Join-Path $actRoot "runs" "output-check-$([guid]::NewGuid().ToString('N'))"
$htmlPath = Join-Path $workspace "index.html"

dotnet build $project -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw "The harness build failed." }

function Assert-OutputCheck {
    param([string]$Name, [int]$ExpectedExitCode)

    $output = & dotnet $assembly check --workspace $workspace 2>&1 | Out-String
    $actualExitCode = $LASTEXITCODE
    if ($actualExitCode -ne $ExpectedExitCode) {
        throw "${Name}: expected exit code $ExpectedExitCode, got $actualExitCode.`n$output"
    }
    Write-Host "PASS: $Name"
}

New-Item -ItemType Directory -Path $workspace | Out-Null
try {
    Assert-OutputCheck "Missing output is an execution failure" 1

    [IO.File]::WriteAllText($htmlPath, "")
    Assert-OutputCheck "Empty output is an execution failure" 1

    [IO.File]::WriteAllText($htmlPath, " `r`n`t")
    Assert-OutputCheck "Whitespace-only output is an execution failure" 1

    $caseVariedFooter = "<html><body><footer>Fictional Demo Data</footer></body></html>"
    [IO.File]::WriteAllText($htmlPath, $caseVariedFooter)
    Assert-OutputCheck "Footer capitalisation does not fail completion" 0
    if ([IO.File]::ReadAllText($htmlPath) -cne $caseVariedFooter) {
        throw "The output check changed the generated file."
    }

    [IO.File]::WriteAllText($htmlPath, "<h2>A model's own design</h2><p>No required footer or article tags.</p>")
    Assert-OutputCheck "Different content and markup do not fail completion" 0

    $task = Get-Content (Join-Path $actRoot "task.json") -Raw | ConvertFrom-Json
    $markdown = [IO.File]::ReadAllText((Join-Path $actRoot "TASK.md"))
    $promptMatch = [regex]::Match($markdown, '(?s)## Exact prompt\s+```text\r?\n(.*?)\r?\n```')
    $markdownPrompt = $promptMatch.Groups[1].Value.Replace("`r`n", "`n")
    $configuredPrompt = $task.prompt.Replace("`r`n", "`n")
    if (-not $promptMatch.Success -or $markdownPrompt -cne $configuredPrompt) {
        throw "TASK.md and task.json prompts differ."
    }
    Write-Host "PASS: TASK.md and task.json prompts match"
}
finally {
    Remove-Item $workspace -Recurse -Force
}
