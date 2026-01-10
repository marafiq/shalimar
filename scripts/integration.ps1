#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Creates IntegrationApp from template and validates the full pipeline.
.DESCRIPTION
    1. Packs all NuGet packages (unless -SkipPack)
    2. Cleans any existing IntegrationApp
    3. Installs template from local artifacts
    4. Creates IntegrationApp via dotnet new shalimar
    5. Builds and verifies all generated files
    6. Runs Playwright E2E tests (unless -SkipTests)
.PARAMETER SkipPack
    Skip packing step, use existing artifacts
.PARAMETER SkipTests
    Skip Playwright E2E tests
.PARAMETER Version
    Package version. Default: 1.0.0-local
#>
param(
    [switch]$SkipPack,
    [switch]$SkipTests,
    [string]$Version = "1.0.0-local"
)
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot ".."
$IntegrationAppDir = Join-Path $RepoRoot "src/Shalimar.IntegrationApp"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"

Write-Host "╔══════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Shalimar Integration Setup          ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════╝" -ForegroundColor Cyan

# Step 1: Pack
if (-not $SkipPack) {
    Write-Host "`n[1/6] Packing..." -ForegroundColor Cyan
    & "$PSScriptRoot/pack.ps1" -Version $Version
} else {
    Write-Host "`n[1/6] Skipping pack (using existing artifacts)..." -ForegroundColor Yellow
    if (-not (Test-Path "$ArtifactsDir/Shalimar.Templates.*.nupkg")) {
        throw "No template package found in artifacts. Run without -SkipPack first."
    }
}

# Step 2: Clean
Write-Host "`n[2/6] Cleaning..." -ForegroundColor Cyan
if (Test-Path $IntegrationAppDir) {
    Write-Host "  Removing existing IntegrationApp..."
    Remove-Item $IntegrationAppDir -Recurse -Force
}

# Clean NuGet cache for shalimar packages
$nugetCache = if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Join-Path $env:USERPROFILE ".nuget/packages"
} else {
    Join-Path $env:HOME ".nuget/packages"
}
Get-ChildItem $nugetCache -Directory -Filter "shalimar*" -ErrorAction SilentlyContinue |
    ForEach-Object {
        Write-Host "  Removing cached package: $($_.Name)"
        Remove-Item $_.FullName -Recurse -Force
    }

# Step 3: Install template
Write-Host "`n[3/6] Installing template..." -ForegroundColor Cyan
dotnet new uninstall Shalimar.Templates 2>$null
$pkg = Get-ChildItem "$ArtifactsDir/Shalimar.Templates.*.nupkg" | Select-Object -First 1
if (-not $pkg) { throw "Template package not found in artifacts" }
Write-Host "  Installing: $($pkg.Name)"
dotnet new install $pkg.FullName --force
if ($LASTEXITCODE -ne 0) { throw "Template install failed" }

# Step 4: Create IntegrationApp
Write-Host "`n[4/6] Creating IntegrationApp..." -ForegroundColor Cyan
Push-Location (Join-Path $RepoRoot "src")
dotnet new shalimar -n Shalimar.IntegrationApp
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "dotnet new shalimar failed" }
Pop-Location

# Add local nuget.config for IntegrationApp
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$ArtifactsDir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content (Join-Path $IntegrationAppDir "nuget.config")

# Step 5: Build IntegrationApp
Write-Host "`n[5/6] Building IntegrationApp..." -ForegroundColor Cyan
Push-Location $IntegrationAppDir

Write-Host "  dotnet restore..."
dotnet restore
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "restore failed" }

Write-Host "  bun install..."
bun install
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "bun install failed" }

Write-Host "  dotnet build..."
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "dotnet build failed" }

Write-Host "  bun run build (vite)..."
bun run build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "vite build failed" }

Pop-Location

# Verify generated files
Write-Host "`nVerifying build outputs..." -ForegroundColor Cyan
$GeneratedDir = Join-Path $IntegrationAppDir "Generated"

# TanStack Router generates this file
$routeTreePath = Join-Path $GeneratedDir "routeTree.gen.ts"
if (Test-Path $routeTreePath) {
    Write-Host "  ✓ Generated/routeTree.gen.ts (TanStack Router)" -ForegroundColor Green
} else {
    throw "Missing: Generated/routeTree.gen.ts - TanStack Router plugin failed"
}

# Vite generates the manifest
$manifestPath = Join-Path $IntegrationAppDir "wwwroot/dist/.vite/manifest.json"
if (Test-Path $manifestPath) {
    Write-Host "  ✓ wwwroot/dist/.vite/manifest.json (Vite)" -ForegroundColor Green
} else {
    throw "Missing: wwwroot/dist/.vite/manifest.json - Vite build failed"
}

# Source generator files (when implemented)
$sourceGenFiles = @(
    "shalimar-routes.g.ts",
    "shalimar-route-defs.g.ts",
    "shalimar-types.g.ts",
    "shalimar-paths.g.ts"
)
$foundSourceGen = 0
foreach ($file in $sourceGenFiles) {
    $filePath = Join-Path $GeneratedDir $file
    if (Test-Path $filePath) {
        Write-Host "  ✓ Generated/$file (Source Generator)" -ForegroundColor Green
        $foundSourceGen++
    }
}
if ($foundSourceGen -eq 0) {
    Write-Host "  ⚠ Source generator files not yet implemented" -ForegroundColor Yellow
}

# Step 6: Run Playwright tests
if (-not $SkipTests) {
    Write-Host "`n[6/6] Running Playwright E2E tests..." -ForegroundColor Cyan
    & "$PSScriptRoot/test.ps1" -Integration
    if ($LASTEXITCODE -ne 0) { throw "Playwright tests failed" }
} else {
    Write-Host "`n[6/6] Skipping Playwright tests (-SkipTests)" -ForegroundColor Yellow
}

Write-Host "`n╔══════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Integration Complete                ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════╝" -ForegroundColor Green
Write-Host "Run: cd src/Shalimar.IntegrationApp && dotnet run" -ForegroundColor Cyan
