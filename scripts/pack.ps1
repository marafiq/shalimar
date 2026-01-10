#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packs all Shalimar NuGet packages.
.PARAMETER Version
    Package version. Default: 1.0.0-local
#>
param(
    [string]$Version = "1.0.0-local"
)
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot ".."
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$RuntimeTsDir = Join-Path $RepoRoot "src/Shalimar.Runtime/ts"
$TemplateRuntimeDir = Join-Path $RepoRoot "src/Shalimar.Templates/templates/shalimar/Shared/runtime"

Write-Host "Packing Shalimar $Version..." -ForegroundColor Cyan

# Clean artifacts
if (Test-Path $ArtifactsDir) { Remove-Item $ArtifactsDir -Recurse -Force }
New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

# Copy TypeScript runtime source to template's Shared folder
Write-Host "[1/3] Updating template runtime..." -ForegroundColor Cyan
if (Test-Path $TemplateRuntimeDir) { Remove-Item $TemplateRuntimeDir -Recurse -Force }
New-Item -ItemType Directory -Path $TemplateRuntimeDir -Force | Out-Null
Copy-Item "$RuntimeTsDir/src/*" $TemplateRuntimeDir -Recurse

# Build .NET projects
Write-Host "[2/3] Building .NET projects..." -ForegroundColor Cyan
dotnet build "$RepoRoot/Shalimar.slnx" -c Release /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# Pack NuGet
Write-Host "[3/3] Packing NuGet packages..." -ForegroundColor Cyan
$projects = @(
    "src/Shalimar.Runtime/Shalimar.Runtime.csproj",
    "src/Shalimar.SourceGenerator/Shalimar.SourceGenerator.csproj",
    "src/Shalimar.MSBuildTasks/Shalimar.MSBuildTasks.csproj",
    "src/Shalimar.RoslynAnalyzer/Shalimar.RoslynAnalyzer.csproj",
    "src/Shalimar.Vite/Shalimar.Vite.csproj",
    "src/Shalimar.Templates/Shalimar.Templates.csproj"
)

foreach ($project in $projects) {
    $projectPath = Join-Path $RepoRoot $project
    dotnet pack $projectPath -c Release -o $ArtifactsDir /p:Version=$Version --no-build
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $project" }
}

Write-Host "`nArtifacts:" -ForegroundColor Green
Get-ChildItem $ArtifactsDir -Filter *.nupkg | ForEach-Object { Write-Host "  $_" }
