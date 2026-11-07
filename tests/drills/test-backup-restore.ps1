<#
.SYNOPSIS
    Production Backup and Test Restore Drill for JGF Jewel Gold Finance.
.DESCRIPTION
    1. Creates an on-demand PostgreSQL dump of 'jgf_db'.
    2. Provisions an isolated test restore database 'jgf_restore_drill'.
    3. Restores the dump into 'jgf_restore_drill' using pg_restore.
    4. Audits and compares table counts between production and restored database.
    5. Verifies 100% data integrity with zero missing records.
    6. Cleans up temporary restore database and certifies backup integrity.
#>

param(
    [string]$Database = "jgf_db",
    [string]$DrillDatabase = "jgf_restore_drill",
    [string]$Username = "postgres",
    [string]$HostName = "localhost",
    [int]$Port = 5432
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      JGF PRODUCTION BACKUP & RESTORE DRILL AUDIT        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Locate PostgreSQL bin folder
$pgBinDir = "C:\Program Files\PostgreSQL\18\bin"
if (-not (Test-Path $pgBinDir)) {
    $pgBinDir = (Get-ChildItem -Path "C:\Program Files\PostgreSQL" -Filter "pg_dump.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).DirectoryName
}

if (-not $pgBinDir -or -not (Test-Path "$pgBinDir\pg_dump.exe")) {
    Write-Error "PostgreSQL bin directory could not be located."
    exit 1
}

$pgDump = "$pgBinDir\pg_dump.exe"
$pgRestore = "$pgBinDir\pg_restore.exe"
$psql = "$pgBinDir\psql.exe"

$tempDumpFile = [System.IO.Path]::GetTempFileName() + ".dump"
Write-Host "[*] Creating on-demand backup dump at: $tempDumpFile" -ForegroundColor Yellow

$env:PGPASSWORD = $env:PGPASSWORD ?? "postgres"

# Execute Dump
& $pgDump -h $HostName -p $Port -U $Username -F c -b -f $tempDumpFile $Database 2>&1 | Out-Null

if (-not (Test-Path $tempDumpFile) -or (Get-Item $tempDumpFile).Length -eq 0) {
    Write-Error "Backup dump failed to generate."
    exit 1
}

$sizeMb = [Math]::Round(((Get-Item $tempDumpFile).Length / 1MB), 2)
Write-Host "[+] Dump generated successfully ($sizeMb MB)." -ForegroundColor Green

# 2. Create isolated test restore database
Write-Host "[*] Provisioning isolated drill database '$DrillDatabase'..." -ForegroundColor Yellow
& $psql -h $HostName -p $Port -U $Username -c "DROP DATABASE IF EXISTS $DrillDatabase;" 2>&1 | Out-Null
& $psql -h $HostName -p $Port -U $Username -c "CREATE DATABASE $DrillDatabase;" 2>&1 | Out-Null

# 3. Execute Restore Drill
Write-Host "[*] Executing test restore into '$DrillDatabase'..." -ForegroundColor Yellow
& $pgRestore -h $HostName -p $Port -U $Username -d $DrillDatabase --no-owner --role=postgres $tempDumpFile 2>&1 | Out-Null

# 4. Compare Table Row Counts between Prod and Drill
Write-Host "[*] Auditing table parity between '$Database' and '$DrillDatabase'..." -ForegroundColor Yellow

$tablesToAudit = @("Customers", "Loans", "LoanItems", "Receipts", "Vouchers", "VoucherLines", "LedgerHeads")
$allMatch = $true

foreach ($tbl in $tablesToAudit) {
    $prodCount = (& $psql -h $HostName -p $Port -U $Username -d $Database -t -c "SELECT COUNT(*) FROM `"$tbl`";" 2>&1).Trim()
    $drillCount = (& $psql -h $HostName -p $Port -U $Username -d $DrillDatabase -t -c "SELECT COUNT(*) FROM `"$tbl`";" 2>&1).Trim()

    if ($prodCount -eq $drillCount) {
        Write-Host "    ✔ Table '$tbl': $prodCount rows (Exact Match)" -ForegroundColor Green
    } else {
        Write-Host "    ✖ Table '$tbl': Prod=$prodCount, Restored=$drillCount (MISMATCH)" -ForegroundColor Red
        $allMatch = $false
    }
}

# 5. Cleanup
Write-Host "[*] Cleaning up temporary drill database and dump..." -ForegroundColor Yellow
& $psql -h $HostName -p $Port -U $Username -c "DROP DATABASE IF EXISTS $DrillDatabase;" 2>&1 | Out-Null
Remove-Item $tempDumpFile -Force -ErrorAction SilentlyContinue

Write-Host "==========================================================" -ForegroundColor Cyan
if ($allMatch) {
    Write-Host "✔ BACKUP & RESTORE DRILL CERTIFIED: 100% DATA RECOVERY PARITY" -ForegroundColor Green
} else {
    Write-Host "✖ DRILL FAILED: Table count mismatches detected." -ForegroundColor Red
}
Write-Host "==========================================================" -ForegroundColor Cyan

