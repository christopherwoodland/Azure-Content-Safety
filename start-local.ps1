[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$KeepExistingProcesses
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSCommandPath
$solutionPath = Join-Path $repoRoot 'NovelCsamDetection.sln'
$functionsProjectPath = Join-Path $repoRoot 'NovelCsam.Functions'
$functionsScriptRoot = Join-Path $functionsProjectPath 'bin\output'
$webProjectPath = Join-Path $repoRoot 'NovelCsam.Web'
$funcExe = 'C:\Program Files\Microsoft\Azure Functions Core Tools\func.exe'

$tempRoot = Join-Path $repoRoot '.tmp-localrun'
$azuritePath = Join-Path $tempRoot 'azurite'
$azuriteLog = Join-Path $tempRoot 'azurite-debug.log'
$functionsOutLog = Join-Path $tempRoot 'functions.out.log'
$functionsErrLog = Join-Path $tempRoot 'functions.err.log'
$webOutLog = Join-Path $tempRoot 'web.out.log'
$webErrLog = Join-Path $tempRoot 'web.err.log'

function Stop-RunningDevProcesses {
    $targets = @('func', 'dotnet', 'node', 'esbuild', 'azurite')
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $targets -contains $_.ProcessName } |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $funcExe)) {
    throw "Azure Functions Core Tools not found at '$funcExe'."
}

if (-not (Test-Path $webProjectPath)) {
    throw "Web project folder not found: '$webProjectPath'."
}

if (-not (Test-Path $functionsProjectPath)) {
    throw "Functions project folder not found: '$functionsProjectPath'."
}

if (-not $KeepExistingProcesses) {
    Write-Host 'Stopping existing local dev processes...' -ForegroundColor Yellow
    Stop-RunningDevProcesses
}

if (-not $NoBuild) {
    Write-Host 'Building solution...' -ForegroundColor Cyan
    & dotnet build $solutionPath --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build failed.'
    }
}

if (-not (Test-Path $functionsScriptRoot)) {
    throw "Functions script root not found: '$functionsScriptRoot'. Run without -NoBuild first."
}

New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
New-Item -ItemType Directory -Force -Path $azuritePath | Out-Null

Write-Host 'Starting Azurite (skip API version check)...' -ForegroundColor Cyan
$azuriteProcess = Start-Process -FilePath 'npx.cmd' `
    -ArgumentList @('azurite', '--silent', '--skipApiVersionCheck', '--location', $azuritePath, '--debug', $azuriteLog) `
    -WorkingDirectory $repoRoot `
    -WindowStyle Hidden `
    -PassThru

Start-Sleep -Seconds 2

Write-Host 'Starting Azure Functions host on http://localhost:7092 ...' -ForegroundColor Cyan
$functionsProcess = Start-Process -FilePath $funcExe `
    -ArgumentList @('start', '--port', '7092', '--no-build', '--script-root', $functionsScriptRoot) `
    -WorkingDirectory $repoRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $functionsOutLog `
    -RedirectStandardError $functionsErrLog `
    -PassThru

Write-Host 'Starting web app on http://127.0.0.1:5173 ...' -ForegroundColor Cyan
$webProcess = Start-Process -FilePath 'npm.cmd' `
    -ArgumentList @('run', 'dev', '--', '--host', '127.0.0.1', '--port', '5173') `
    -WorkingDirectory $webProjectPath `
    -WindowStyle Hidden `
    -RedirectStandardOutput $webOutLog `
    -RedirectStandardError $webErrLog `
    -PassThru

Start-Sleep -Seconds 2

Write-Host ''
Write-Host 'Local stack started.' -ForegroundColor Green
Write-Host "  Azurite PID:   $($azuriteProcess.Id)"
Write-Host "  Functions PID: $($functionsProcess.Id)"
Write-Host "  Web PID:       $($webProcess.Id)"
Write-Host ''
Write-Host 'Endpoints:'
Write-Host '  Web:       http://127.0.0.1:5173'
Write-Host '  Functions: http://localhost:7092/api/AnalyzeFrames_HttpStart'
Write-Host ''
Write-Host 'Logs:'
Write-Host "  $azuriteLog"
Write-Host "  $functionsOutLog"
Write-Host "  $functionsErrLog"
Write-Host "  $webOutLog"
Write-Host "  $webErrLog"
Write-Host ''
Write-Host 'To stop: run this in PowerShell from repo root:'
Write-Host "  Get-Process | Where-Object ProcessName -in @('azurite','func','dotnet','node','esbuild') | Stop-Process -Force"
