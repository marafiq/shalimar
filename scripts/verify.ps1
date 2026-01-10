#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Zero-step verification for the entire Shalimar repo.
.DESCRIPTION
    Runs a reliable, end-to-end pipeline:
      1) Restore (dotnet + bun)
      2) Build (Debug)
      3) Unit tests
      4) Integration pipeline (pack -> dotnet new -> IntegrationApp build -> optional Playwright)
.PARAMETER SkipIntegration
    Skip the template -> IntegrationApp pipeline.
.PARAMETER SkipE2E
    Skip Playwright E2E tests (still builds IntegrationApp via template).
.PARAMETER Version
    Local package version used for pack/template install. Default: 1.0.0-local
.PARAMETER Configuration
    Build configuration for solution build (Debug or Release). Default: Debug
#>
param(
    [switch]$SkipIntegration,
    [switch]$SkipE2E,
    [string]$Version = "1.0.0-local",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$VerifyTsPropagation = $true
)

$ErrorActionPreference = "Stop"
$RepoRoot = Join-Path $PSScriptRoot ".."

function Require-Command([string]$name, [string]$hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Missing prerequisite '$name'. $hint"
    }
}

function Acquire-VerifyLock() {
    $lockDir = Join-Path ([System.IO.Path]::GetTempPath()) "shalimar-verify.lock"
    try {
        [System.IO.Directory]::CreateDirectory($lockDir) | Out-Null
        $pidPath = Join-Path $lockDir "pid"
        Set-Content -Path $pidPath -Value $PID -NoNewline
        return $lockDir
    } catch {
        throw "Another verify run is already in progress (lock: $lockDir)."
    }
}

function Release-VerifyLock([string]$lockDir) {
    try { Remove-Item $lockDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
}

Write-Host "Shalimar verify (zero-step)..." -ForegroundColor Cyan
Write-Host "  Repo: $RepoRoot"
Write-Host "  Configuration: $Configuration"
Write-Host "  Version: $Version"
Write-Host "  SkipIntegration: $SkipIntegration"
Write-Host "  SkipE2E: $SkipE2E"

Require-Command dotnet "Install the .NET SDK pinned by global.json."
Require-Command bun "Install bun (https://bun.sh/) for Vite/TS tooling."

 $lock = Acquire-VerifyLock
 try {
Write-Host "`n[1/4] Restore" -ForegroundColor Cyan
& "$PSScriptRoot/restore.ps1"

Write-Host "`n[2/4] Build" -ForegroundColor Cyan
& "$PSScriptRoot/build.ps1" -Configuration $Configuration

Write-Host "`n[3/4] Unit tests" -ForegroundColor Cyan
& "$PSScriptRoot/test.ps1" -Unit

if (-not $SkipIntegration) {
    Write-Host "`n[4/4] Integration pipeline (template -> IntegrationApp)" -ForegroundColor Cyan
    $marker = if ($VerifyTsPropagation) { "SHALIMAR_VERIFY_TS_PROPAGATION__$(Get-Date -Format 'yyyyMMddHHmmssfff')" } else { $null }
    if ($SkipE2E) {
        if ($marker) {
            & "$PSScriptRoot/integration.ps1" -Version $Version -SkipTests -VerifyTsMarker $marker
        } else {
            & "$PSScriptRoot/integration.ps1" -Version $Version -SkipTests
        }
    } else {
        if ($marker) {
            & "$PSScriptRoot/integration.ps1" -Version $Version -VerifyTsMarker $marker
        } else {
            & "$PSScriptRoot/integration.ps1" -Version $Version
        }
    }
} else {
    Write-Host "`n[4/4] Skipping integration pipeline (-SkipIntegration)" -ForegroundColor Yellow
}

Write-Host "`n✓ Verify complete" -ForegroundColor Green
 } finally {
     Release-VerifyLock $lock
 }
