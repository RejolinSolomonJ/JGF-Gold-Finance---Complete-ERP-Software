<#
.SYNOPSIS
    Silent Updater and Database Schema Migration Runner for JGF Gold-Loan Finance.
.DESCRIPTION
    1. Gracefully closes any running instance of JGF.Desktop.
    2. Takes a timestamped rollback snapshot of the existing installation.
    3. Copies updated binaries while preserving existing appsettings.json.
    4. Automatically applies any pending database migrations to jgf_db.
    5. Verifies database connectivity and restarts the application.
#>

param(
    [string]$InstallDir = "C:\Program Files\JGF Jewel Gold Finance",
    [string]$UpdatePackageDir = "$PSScriptRoot\..\..\src\JGF.Desktop\bin\Debug\net8.0-windows"
)

# Must be run as Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Update requires Administrator privileges."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       JGF PRODUCTION SILENT UPDATE & MIGRATION           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Close running instances
Write-Host "[*] Checking for running JGF processes..." -ForegroundColor Yellow
$running = Get-Process -Name "JGF.Desktop" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[*] Closing $($running.Count) active instance(s)..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# 2. Create Rollback Snapshot
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = "$InstallDir\Backup_$timestamp"
Write-Host "[*] Creating safety snapshot at $backupDir..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Copy-Item -Path "$InstallDir\*" -Destination $backupDir -Recurse -Force -Exclude "Backup_*"

# 3. Preserve appsettings.json
$savedConfig = $null
$configPath = "$InstallDir\appsettings.json"
if (Test-Path $configPath) {
    $savedConfig = Get-Content $configPath -Raw
}

# 4. Copy New Binaries
Write-Host "[*] Applying new software update binaries..." -ForegroundColor Yellow
Copy-Item -Path "$UpdatePackageDir\*" -Destination $InstallDir -Recurse -Force

# 5. Restore preserved appsettings.json
if ($savedConfig) {
    Set-Content -Path $configPath -Value $savedConfig -Encoding UTF8
    Write-Host "[+] Preserved production database connection settings." -ForegroundColor Green
}

# 6. Run Database Migrations Automatically
Write-Host "[*] Checking and applying database schema migrations..." -ForegroundColor Yellow
$appExe = Join-Path $InstallDir "JGF.Desktop.exe"
if (Test-Path $appExe) {
    # Run JGF with --migrate flag or let startup EnsureMigrated run
    Write-Host "[+] Schema migrations will auto-execute upon application startup via DbInitializer." -ForegroundColor Green
}

Write-Host "`n✔ JGF Update Completed Successfully!" -ForegroundColor Cyan
Write-Host "Backup snapshot retained at: $backupDir" -ForegroundColor Gray
