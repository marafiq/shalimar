#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs Shalimar tests.
.PARAMETER Unit
    Run only unit tests (non-Integration)
.PARAMETER Integration
    Run only Playwright integration tests
.PARAMETER Port
    Port for IntegrationApp. Default: 5099
#>
param(
    [switch]$Unit,
    [switch]$Integration,
    [int]$Port = 5099
)
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot ".."
$AppDir = Join-Path $RepoRoot "src/Shalimar.IntegrationApp"

# If neither specified, run both
$runUnit = $Unit -or (-not $Unit -and -not $Integration)
$runIntegration = $Integration -or (-not $Unit -and -not $Integration)

if ($runUnit) {
    Write-Host "Running unit tests..." -ForegroundColor Cyan
    dotnet test "$RepoRoot/Shalimar.slnx" --filter "Category!=Integration" --no-build
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed" }
}

if ($runIntegration) {
    Write-Host "`nRunning Playwright integration tests..." -ForegroundColor Cyan

    if (-not (Test-Path $AppDir)) {
        throw "IntegrationApp not found. Run ./scripts/integration.ps1 first"
    }

    # Install Playwright browsers if needed
    Write-Host "  Ensuring Playwright browsers are installed..."
    $playwrightDir = Join-Path $RepoRoot "tests/Shalimar.IntegrationPlaywrightTests"
    Push-Location $playwrightDir
    dotnet build --no-restore 2>$null
    $pwScript = Join-Path $playwrightDir "bin/Debug/net10.0/playwright.ps1"
    if (Test-Path $pwScript) {
        & $pwScript install chromium 2>$null
    }
    Pop-Location

    # Start IntegrationApp
    Write-Host "  Starting IntegrationApp on port $Port..."
    Push-Location $AppDir
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:ASPNETCORE_ENVIRONMENT = "Production"

    $logFile = Join-Path $AppDir "app.log"
    # Cross-platform: use Start-Process without -WindowStyle on non-Windows
    if ($IsWindows) {
        $process = Start-Process dotnet -ArgumentList "run","--no-build" -PassThru -RedirectStandardOutput $logFile -RedirectStandardError "$logFile.err" -WindowStyle Hidden
    } else {
        $process = Start-Process dotnet -ArgumentList "run","--no-build" -PassThru -RedirectStandardOutput $logFile -RedirectStandardError "$logFile.err"
    }

    # Wait for app to be ready
    $ready = $false
    Write-Host "  Waiting for app to start..."
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep 1
        try {
            $response = Invoke-WebRequest "http://localhost:$Port" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch { }
    }

    if (-not $ready) {
        if (-not $process.HasExited) { $process.Kill() }
        Pop-Location
        Write-Host "App log:" -ForegroundColor Yellow
        if (Test-Path $logFile) { Get-Content $logFile -Tail 20 }
        throw "IntegrationApp failed to start within 30 seconds"
    }

    Write-Host "  App is ready!" -ForegroundColor Green
    Pop-Location

    try {
        $env:INTEGRATION_APP_URL = "http://localhost:$Port"
        dotnet test "$RepoRoot/tests/Shalimar.IntegrationPlaywrightTests" --filter "Category=Integration" --no-build
        $testResult = $LASTEXITCODE
    } finally {
        Write-Host "  Stopping IntegrationApp..."
        if (-not $process.HasExited) { $process.Kill() }
    }

    if ($testResult -ne 0) { throw "Integration tests failed" }
}

Write-Host "`n✓ All tests passed" -ForegroundColor Green
