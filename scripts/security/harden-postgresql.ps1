<#
.SYNOPSIS
    Hardens PostgreSQL for JGF Gold-Loan Finance Software in a 2-PC Production Environment.
.DESCRIPTION
    1. Binds PostgreSQL to listen on localhost and the PC1 LAN IP.
    2. Configures pg_hba.conf to whitelist ONLY PC1 (127.0.0.1) and PC2's specific IP address via scram-sha-256.
    3. Creates a least-privilege database user 'jgf_app' with access restricted strictly to DML on the jgf_db schema.
    4. Enables SSL/TLS encryption for database communication between PC2 and PC1.
.PARAMETER Pc1LanIp
    The static LAN IP address of PC1 (Server, e.g. 192.168.1.100).
.PARAMETER Pc2LanIp
    The static LAN IP address of PC2 (Client, e.g. 192.168.1.101).
.PARAMETER AppPassword
    The strong password for the limited-privilege 'jgf_app' role.
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$Pc1LanIp = "192.168.1.100",

    [Parameter(Mandatory=$false)]
    [string]$Pc2LanIp = "192.168.1.101",

    [Parameter(Mandatory=$false)]
    [string]$AppPassword = "JgfSecure_AppPassword2026!"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   JGF PRODUCTION SECURITY HARDENING - POSTGRESQL & LAN   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Locate PostgreSQL Installation & Data Directory
$pgDataCandidates = @(
    "C:\Program Files\PostgreSQL\18\data",
    "C:\Program Files\PostgreSQL\17\data",
    "C:\Program Files\PostgreSQL\16\data",
    "C:\Program Files\PostgreSQL\15\data"
)

$pgDataDir = $null
foreach ($path in $pgDataCandidates) {
    if (Test-Path $path) {
        $pgDataDir = $path
        break
    }
}

if (-not $pgDataDir) {
    Write-Warning "Could not automatically locate PostgreSQL data folder. Please specify data folder."
    $pgDataDir = "C:\Program Files\PostgreSQL\18\data"
}

Write-Host "[+] Using PostgreSQL Data Directory: $pgDataDir" -ForegroundColor Green

# 2. Update postgresql.conf: Listen Addresses & SSL
$pgConfPath = Join-Path $pgDataDir "postgresql.conf"
if (Test-Path $pgConfPath) {
    Write-Host "[*] Hardening postgresql.conf..." -ForegroundColor Yellow
    $conf = Get-Content $pgConfPath -Raw

    # Bind listen_addresses
    $listenConfig = "listen_addresses = 'localhost, $Pc1LanIp'"
    if ($conf -match "(?m)^listen_addresses\s*=.*") {
        $conf = $conf -replace "(?m)^listen_addresses\s*=.*", $listenConfig
    } else {
        $conf += "`n$listenConfig`n"
    }

    # Ensure SSL is turned on
    if ($conf -match "(?m)^ssl\s*=.*") {
        $conf = $conf -replace "(?m)^ssl\s*=.*", "ssl = on"
    } else {
        $conf += "`nssl = on`n"
    }

    Set-Content -Path $pgConfPath -Value $conf -Encoding UTF8
    Write-Host "[+] postgresql.conf updated: listening only on localhost and $Pc1LanIp with SSL enabled." -ForegroundColor Green
}

# 3. Update pg_hba.conf: Whitelist only PC1 and PC2 with scram-sha-256
$pgHbaPath = Join-Path $pgDataDir "pg_hba.conf"
if (Test-Path $pgHbaPath) {
    Write-Host "[*] Hardening pg_hba.conf access control list..." -ForegroundColor Yellow
    
    # Backup current pg_hba.conf
    Copy-Item $pgHbaPath "$pgHbaPath.bak_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

    $hbaRules = @"
# ─────────────────────────────────────────────────────────────────────────────
# JGF Production Hardened Client Authentication Configuration
# Restricts database connectivity strictly to PC1 and PC2
# ─────────────────────────────────────────────────────────────────────────────

# Local administrative connections (postgres user only from localhost)
host    postgres        postgres        127.0.0.1/32            scram-sha-256
host    postgres        postgres        ::1/128                 scram-sha-256

# PC1 Application connection
host    jgf_db          jgf_app         127.0.0.1/32            scram-sha-256
host    jgf_db          jgf_app         $Pc1LanIp/32            scram-sha-256

# PC2 Counter Client connection (restricted to PC2 LAN IP)
host    jgf_db          jgf_app         $Pc2LanIp/32            scram-sha-256

# Reject all other hosts and unauthorized subnets
host    all             all             all                     reject
"@

    Set-Content -Path $pgHbaPath -Value $hbaRules -Encoding UTF8
    Write-Host "[+] pg_hba.conf locked to PC1 (127.0.0.1, $Pc1LanIp) and PC2 ($Pc2LanIp) using scram-sha-256." -ForegroundColor Green
}

# 4. Generate SQL Script to Create Limited-Privilege Role 'jgf_app'
$roleSqlPath = Join-Path $PSScriptRoot "create_jgf_app_role.sql"
$roleSql = @"
-- ============================================================================
-- JGF Limited-Privilege Application Role
-- Revokes administrative capabilities and grants only DML on application schema
-- ============================================================================

DO `$\$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'jgf_app') THEN
        CREATE ROLE jgf_app WITH LOGIN PASSWORD '$AppPassword' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
    ELSE
        ALTER ROLE jgf_app WITH PASSWORD '$AppPassword';
    END IF;
END
`$\$;

-- Connect permissions
GRANT CONNECT ON DATABASE jgf_db TO jgf_app;

-- Schema usage
\c jgf_db
GRANT USAGE ON SCHEMA public TO jgf_app;

-- Table permissions: Allow CRUD, but prevent DROP TABLE, TRUNCATE, or DDL
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO jgf_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO jgf_app;

-- Default permissions for newly created tables (migrations)
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO jgf_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO jgf_app;

-- Revoke dangerous access
REVOKE CREATE ON SCHEMA public FROM jgf_app;
"@

Set-Content -Path $roleSqlPath -Value $roleSql -Encoding UTF8
Write-Host "[+] Generated role provisioning script: $roleSqlPath" -ForegroundColor Green

# 5. Restart PostgreSQL Service to apply changes
Write-Host "[*] Restarting PostgreSQL service to apply security configuration..." -ForegroundColor Yellow
$pgService = Get-Service -Name *postgres* | Select-Object -First 1
if ($pgService) {
    Restart-Service -Name $pgService.Name -Force
    Write-Host "[+] PostgreSQL service ($($pgService.Name)) restarted successfully." -ForegroundColor Green
}

Write-Host "`n✔ PostgreSQL Hardening Complete!" -ForegroundColor Cyan
Write-Host "Next Step: Run 'psql -U postgres -f create_jgf_app_role.sql' to apply the jgf_app role." -ForegroundColor Yellow
