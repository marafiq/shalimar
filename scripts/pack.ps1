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

Write-Host "Packing Shalimar $Version..." -ForegroundColor Cyan

# Clean artifacts
if (Test-Path $ArtifactsDir) { Remove-Item $ArtifactsDir -Recurse -Force }
New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

# Build .NET projects
Write-Host "[1/2] Building .NET projects..." -ForegroundColor Cyan
dotnet build "$RepoRoot/Shalimar.slnx" -c Release /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# Pack NuGet
Write-Host "[2/2] Packing NuGet packages..." -ForegroundColor Cyan
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
