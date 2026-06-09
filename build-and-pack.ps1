#!/usr/bin/env pwsh
# build-and-pack.ps1
#
# Builds the azure-api-management-policy-toolkit, packs NuGet packages,
# and optionally copies them to the sky-api-management packages folder.
#
# Usage:
#   .\build-and-pack.ps1                          # build, pack, copy to sky-api-management
#   .\build-and-pack.ps1 -SkipCopy                # build and pack only
#   .\build-and-pack.ps1 -SkyApiManagementPath "C:\other\sky-api-management"

param(
    [switch]$SkipCopy,
    [string]$SkyApiManagementPath = "C:\code\sky-api-management"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "=== Building azure-api-management-policy-toolkit ===" -ForegroundColor Cyan

Push-Location $scriptDir
try {
    # Build with --no-incremental to ensure changes in any project are picked up
    Write-Host "Building (Release)..." -ForegroundColor Yellow
    dotnet build -c Release --no-incremental -v q
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    Write-Host "Packing..." -ForegroundColor Yellow
    dotnet pack -c Release -v q
    if ($LASTEXITCODE -ne 0) { throw "Pack failed" }

    $outputDir = Join-Path $scriptDir "output"
    $nupkgFiles = Get-ChildItem "$outputDir\*.nupkg"
    Write-Host "Packed $($nupkgFiles.Count) package(s) to $outputDir" -ForegroundColor Green
    $nupkgFiles | ForEach-Object { Write-Host "  $($_.Name)" }

    if (-not $SkipCopy) {
        $packagesDir = Join-Path $SkyApiManagementPath "packages"
        if (-not (Test-Path $packagesDir)) {
            throw "sky-api-management packages directory not found: $packagesDir. Use -SkipCopy or -SkyApiManagementPath."
        }

        Write-Host ""
        Write-Host "Copying packages to $packagesDir..." -ForegroundColor Yellow

        # Clear NuGet cache for decompiling, compiling, and authoring to force re-install of new version
        $cachesToClear = @(
            "$env:USERPROFILE\.nuget\packages\decompiling",
            "$env:USERPROFILE\.nuget\packages\microsoft.azure.apimanagement.policytoolkit.compiling",
            "$env:USERPROFILE\.nuget\packages\microsoft.azure.apimanagement.policytoolkit.authoring"
        )
        foreach ($cachePath in $cachesToClear) {
            if (Test-Path $cachePath) {
                $name = Split-Path $cachePath -Leaf
                Write-Host "  Clearing NuGet cache for $name..." -ForegroundColor Gray
                Remove-Item $cachePath -Recurse -Force
            }
        }
        $resolverCachePath = "$env:USERPROFILE\.dotnet\toolResolverCache\1\decompiling"
        if (Test-Path $resolverCachePath) {
            Remove-Item $resolverCachePath -Recurse -Force
        }
        $compilerResolverCachePath = "$env:USERPROFILE\.dotnet\toolResolverCache\1\microsoft.azure.apimanagement.policytoolkit.compiling"
        if (Test-Path $compilerResolverCachePath) {
            Remove-Item $compilerResolverCachePath -Recurse -Force
        }

        Copy-Item "$outputDir\*.nupkg" $packagesDir -Force
        Write-Host "Packages copied. Run update-policies.ps1 in sky-api-management to regenerate C# files." -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
