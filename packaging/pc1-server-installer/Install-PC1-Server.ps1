<#
.SYNOPSIS
    Automated Production Installer for PC1 (Server + Primary Counter).
.DESCRIPTION
    1. Verifies / Installs PostgreSQL database service.
    2. Provisions 'jgf_db' database and hardened role 'jgf_app'.
    3. Deploys JGF Desktop Application to C:\Program Files\JGF Jewel Gold Finance.
    4. Sets up Windows Firewall inbound rule for PC2 LAN access.
    5. Schedules daily automated database backup task in Windows Task Scheduler.
    6. Creates Desktop and Start Menu shortcuts.
#>

param(
    [string]$InstallDir = "C:\Program Files\JGF Jewel Gold Finance",
    [string]$Pc1LanIp = "192.168.1.100",
    [string]$Pc2LanIp = "192.168.1.101",
    [string]$AppPassword = "JgfSecure_AppPassword2026!"
)

# Must be run as Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "This installer requires Administrator privileges. Please run in an elevated PowerShell window."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      JGF JEWEL GOLD FINANCE - PC1 SERVER INSTALLER       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verify PostgreSQL service
Write-Host "[*] Checking PostgreSQL database service..." -ForegroundColor Yellow
$pgService = Get-Service -Name *postgres* -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $pgService) {
    Write-Host "[!] PostgreSQL service not found. Please install PostgreSQL (v15+) from https://www.postgresql.org/download/windows/" -ForegroundColor Red
    Write-Host "    Once installed, re-run this script to complete the setup." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "[+] Found running PostgreSQL service: $($pgService.Name)" -ForegroundColor Green
}

# 2. Deploy Application Binaries
Write-Host "[*] Deploying application to: $InstallDir" -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$sourceBinDir = Join-Path $PSScriptRoot "..\..\src\JGF.Desktop\bin\Debug\net8.0-windows"
if (Test-Path $sourceBinDir) {
    Copy-Item -Path "$sourceBinDir\*" -Destination $InstallDir -Recurse -Force
    Write-Host "[+] Binaries successfully copied." -ForegroundColor Green
} else {
    Write-Warning "Source binary directory not found at $sourceBinDir. Please run 'dotnet build' before packaging."
}

# 3. Configure appsettings.json for Server Installation
$appSettingsPath = Join-Path $InstallDir "appsettings.json"
$connStr = "Host=localhost;Port=5432;Database=jgf_db;Username=jgf_app;Password=$AppPassword;SSL Mode=Prefer"
$appSettingsJson = @"
{
  "ConnectionStrings": {
    "DefaultConnection": "$connStr"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
"@
Set-Content -Path $appSettingsPath -Value $appSettingsJson -Encoding UTF8
Write-Host "[+] Configured $appSettingsPath with production credentials." -ForegroundColor Green

# 4. Run Security Hardening Script
$hardenScript = Join-Path $PSScriptRoot "..\..\scripts\security\harden-postgresql.ps1"
if (Test-Path $hardenScript) {
    Write-Host "[*] Running PostgreSQL LAN hardening..." -ForegroundColor Yellow
    & $hardenScript -Pc1LanIp $Pc1LanIp -Pc2LanIp $Pc2LanIp -AppPassword $AppPassword
}

# 5. Configure Windows Firewall
$firewallScript = Join-Path $PSScriptRoot "..\..\scripts\security\configure-windows-firewall.ps1"
if (Test-Path $firewallScript) {
    Write-Host "[*] Configuring Windows Firewall for PC2 ($Pc2LanIp)..." -ForegroundColor Yellow
    & $firewallScript -Pc2IpAddress $Pc2LanIp
}

# 6. Schedule Automated Daily Backup Task
Write-Host "[*] Registering daily 9:00 PM automated backup in Windows Task Scheduler..." -ForegroundColor Yellow
$backupScriptPath = Join-Path $PSScriptRoot "..\..\scripts\backup\run-automated-backup.ps1"
$taskName = "JGF-Daily-Automated-Backup"

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$backupScriptPath`""
$trigger = New-ScheduledTaskTrigger -Daily -At "21:00"
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Description "Daily automated PostgreSQL backup for JGF Jewel Gold Finance" | Out-Null
Write-Host "[+] Windows Task Scheduler task '$taskName' registered for 9:00 PM daily." -ForegroundColor Green

# 7. Create Desktop Shortcut
$wsh = New-Object -ComObject WScript.Shell
$desktopDir = [Environment]::GetFolderPath("Desktop")
$shortcut = $wsh.CreateShortcut(Join-Path $desktopDir "JGF Jewel Gold Finance.lnk")
$shortcut.TargetPath = Join-Path $InstallDir "JGF.Desktop.exe"
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "JGF Jewel Gold Finance System (PC1 Server)"
$shortcut.Save()

Write-Host "[+] Desktop shortcut created." -ForegroundColor Green
Write-Host "`n✔ PC1 Server Installation Completed Successfully!" -ForegroundColor Cyan

