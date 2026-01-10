#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds all Shalimar projects.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Debug
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot ".."

Write-Host "Building Shalimar ($Configuration)..." -ForegroundColor Cyan

# Build .NET
dotnet build "$RepoRoot/Shalimar.slnx" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Write-Host "`n✓ Build complete" -ForegroundColor Green
