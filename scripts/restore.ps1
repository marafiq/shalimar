#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Restores all dependencies for Shalimar development.
.DESCRIPTION
    Restores .NET packages and bun dependencies.
#>
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot ".."

Write-Host "Restoring Shalimar dependencies..." -ForegroundColor Cyan

# .NET restore
Write-Host "[1/2] Restoring .NET packages..." -ForegroundColor Cyan
dotnet restore "$RepoRoot/Shalimar.slnx"
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

# Bun install
Write-Host "[2/2] Installing bun dependencies..." -ForegroundColor Cyan
Push-Location $RepoRoot
bun install
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "bun install failed" }
Pop-Location

Write-Host "`n✓ All dependencies restored" -ForegroundColor Green
