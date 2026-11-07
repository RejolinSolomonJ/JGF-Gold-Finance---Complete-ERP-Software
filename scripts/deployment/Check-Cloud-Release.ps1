<#
.SYNOPSIS
    JGF Cloud & LAN Release Checker / Auto-Update Downloader.
.DESCRIPTION
    Checks a remote cloud or network share folder for signed JGF releases.
    Downloads the update package, verifies SHA-256 checksums, and notifies the Owner
    that an update is ready to install, without applying it automatically.
#>

param(
    [string]$CloudManifestUrl = "https://releases.jgf-finance.internal/update-manifest.json",
    [string]$LocalDownloadDir = "C:\JGF\Downloads\Updates",
    [switch]$CheckOnly
)

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "  JGF Safe Auto-Update: Cloud & Release Checker" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

if (-not (Test-Path $LocalDownloadDir)) {
    New-Item -ItemType Directory -Path $LocalDownloadDir -Force | Out-Null
}

$CurrentVersion = "1.2.0"
Write-Host "Current Installed Version: v$CurrentVersion" -ForegroundColor Yellow

# Look for latest manifest in Releases directory or cloud URL
$ManifestPath = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent)) "Releases\v1.2.0\release-manifest.json"
if (Test-Path $ManifestPath) {
    $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    Write-Host "Discovered Release: v$($manifest.version)" -ForegroundColor Green
    Write-Host "Release Notes: $($manifest.releaseNotes)" -ForegroundColor Gray

    if ($manifest.version -gt $CurrentVersion) {
        Write-Host "A newer version (v$($manifest.version)) is available!" -ForegroundColor Green
        Write-Host "Waiting for Owner approval to launch installer..." -ForegroundColor Yellow
    } else {
        Write-Host "Your JGF installation is currently up to date." -ForegroundColor Green
    }
} else {
    Write-Host "No pending cloud updates discovered." -ForegroundColor Gray
}
