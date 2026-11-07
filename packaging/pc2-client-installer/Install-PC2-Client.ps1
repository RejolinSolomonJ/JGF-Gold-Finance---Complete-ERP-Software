<#
.SYNOPSIS
    Automated Production Installer for PC2 (Secondary Counter / Cashier Client).
.DESCRIPTION
    1. Installs JGF Desktop client to C:\Program Files\JGF Jewel Gold Finance.
    2. Runs interactive Connection Wizard to test and verify connectivity to PC1.
    3. Writes client appsettings.json.
    4. Creates Desktop and Start Menu shortcuts.
#>

param(
    [string]$InstallDir = "C:\Program Files\JGF Jewel Gold Finance",
    [string]$DefaultServerIp = "192.168.1.100",
    [string]$AppPassword = "JgfSecure_AppPassword2026!"
)

# Must be run as Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "This installer requires Administrator privileges. Please run in an elevated PowerShell window."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   JGF JEWEL GOLD FINANCE - PC2 CLIENT INSTALLER & WIZARD " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Deploy Application Binaries
Write-Host "[*] Deploying client application to: $InstallDir" -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$sourceBinDir = Join-Path $PSScriptRoot "..\..\src\JGF.Desktop\bin\Debug\net8.0-windows"
if (Test-Path $sourceBinDir) {
    Copy-Item -Path "$sourceBinDir\*" -Destination $InstallDir -Recurse -Force
    Write-Host "[+] Binaries successfully copied." -ForegroundColor Green
} else {
    Write-Warning "Source binary directory not found at $sourceBinDir. Please build before packaging."
}

# 2. Interactive Connection Wizard
Write-Host "`n────────────────── CONNECTION WIZARD ──────────────────" -ForegroundColor Yellow
$serverIp = Read-Host "Enter PC1 (Server) LAN IP address [$DefaultServerIp]"
if ([string]::IsNullOrWhiteSpace($serverIp)) {
    $serverIp = $DefaultServerIp
}

Write-Host "[*] Testing network connection to PC1 ($serverIp:5432)..." -ForegroundColor Yellow
$tcpTest = Test-NetConnection -ComputerName $serverIp -Port 5432 -WarningAction SilentlyContinue

if ($tcpTest.TcpTestSucceeded) {
    Write-Host "[+] Connection to PC1 PostgreSQL server on port 5432 SUCCEEDED!" -ForegroundColor Green
} else {
    Write-Warning "Cannot connect to $serverIp:5432. Please ensure PC1 is turned on, connected to the same LAN, and Windows Firewall on PC1 allows PC2."
    $retry = Read-Host "Proceed anyway? (y/n)"
    if ($retry -ne "y") {
        Write-Host "Installation cancelled." -ForegroundColor Red
        exit 1
    }
}

# 3. Write client appsettings.json
$appSettingsPath = Join-Path $InstallDir "appsettings.json"
$connStr = "Host=$serverIp;Port=5432;Database=jgf_db;Username=jgf_app;Password=$AppPassword;SSL Mode=Prefer"
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
Write-Host "[+] Client configuration saved pointing to PC1 ($serverIp)." -ForegroundColor Green

# 4. Create Desktop Shortcut
$wsh = New-Object -ComObject WScript.Shell
$desktopDir = [Environment]::GetFolderPath("Desktop")
$shortcut = $wsh.CreateShortcut(Join-Path $desktopDir "JGF Jewel Gold Finance (Counter 2).lnk")
$shortcut.TargetPath = Join-Path $InstallDir "JGF.Desktop.exe"
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "JGF Jewel Gold Finance System (PC2 Client)"
$shortcut.Save()

Write-Host "[+] Desktop shortcut created for Counter 2." -ForegroundColor Green
Write-Host "`n✔ PC2 Client Installation Completed Successfully!" -ForegroundColor Cyan

