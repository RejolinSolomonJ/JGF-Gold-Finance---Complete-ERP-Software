<#
.SYNOPSIS
    Configures Windows Advanced Firewall for JGF Gold-Loan Finance 2-PC LAN deployment.
.DESCRIPTION
    Creates strict inbound firewall rules allowing TCP port 5432 (PostgreSQL) ONLY
    from PC2's specific LAN IP and localhost, while blocking all public network access.
.PARAMETER Pc2IpAddress
    The static LAN IP address of PC2 (e.g., 192.168.1.101).
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$Pc2IpAddress = "192.168.1.101"
)

# Must be run as Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "This script requires Administrator privileges. Please re-run in an elevated PowerShell session."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   JGF WINDOWS ADVANCED FIREWALL CONFIGURATION (2-PC LAN) " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$RuleName = "JGF-PostgreSQL-PC2-Only"

# 1. Remove existing broad PostgreSQL rules if present
Write-Host "[*] Auditing existing firewall rules for port 5432..." -ForegroundColor Yellow
$existingRules = Get-NetFirewallRule -DisplayName "*PostgreSQL*" -ErrorAction SilentlyContinue
foreach ($rule in $existingRules) {
    Write-Host "[-] Removing broad legacy rule: $($rule.DisplayName)" -ForegroundColor DarkYellow
    Remove-NetFirewallRule -Name $rule.Name -ErrorAction SilentlyContinue
}

# 2. Add restricted inbound rule for PC2 and local loopback
Write-Host "[*] Creating restricted firewall rule for PC2 IP ($Pc2IpAddress)..." -ForegroundColor Yellow

New-NetFirewallRule -Name $RuleName `
                    -DisplayName "JGF Jewel Finance - PostgreSQL (PC2 Allowed Only)" `
                    -Description "Allows incoming PostgreSQL database traffic exclusively from PC2 ($Pc2IpAddress) on private LAN." `
                    -Direction Inbound `
                    -Action Allow `
                    -Protocol TCP `
                    -LocalPort 5432 `
                    -RemoteAddress @("127.0.0.1", "::1", $Pc2IpAddress) `
                    -Profile Private, Domain `
                    -Enabled True

Write-Host "[+] Inbound Firewall rule '$RuleName' created successfully." -ForegroundColor Green
Write-Host "    - Port: 5432 (TCP)" -ForegroundColor Gray
Write-Host "    - Allowed Remotes: 127.0.0.1 (PC1 Local), $Pc2IpAddress (PC2 LAN)" -ForegroundColor Gray
Write-Host "    - Network Profiles: Domain & Private ONLY (Public networks completely blocked)" -ForegroundColor Gray
Write-Host "`n✔ Firewall Hardening Complete!" -ForegroundColor Cyan

