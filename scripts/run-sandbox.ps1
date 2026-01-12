#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Build and run Shalimar.SandboxApp locally (Production, hashed assets).
#>

$ErrorActionPreference = "Stop"
$RepoRoot = Join-Path $PSScriptRoot ".."
$AppDir = Join-Path $RepoRoot "src/Shalimar.SandboxApp"

Write-Host "==> Restore" -ForegroundColor Cyan
& "$RepoRoot/scripts/restore.ps1"
if ($LASTEXITCODE -ne 0) { throw "restore failed" }

Write-Host "==> Build (generates TS into Generated/)" -ForegroundColor Cyan
Push-Location $AppDir
dotnet build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "dotnet build failed" }

Write-Host "==> Build frontend assets to wwwroot/dist" -ForegroundColor Cyan
bun install
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "bun install failed" }
bun run build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "bun run build failed" }

Write-Host "==> Run (Production env, uses hashed assets)" -ForegroundColor Green
$env:ASPNETCORE_ENVIRONMENT = "Production"
if (-not $env:ASPNETCORE_URLS) { $env:ASPNETCORE_URLS = "http://localhost:5099" }
Write-Host "Open: $env:ASPNETCORE_URLS" -ForegroundColor Yellow
dotnet run --no-build
Pop-Location

