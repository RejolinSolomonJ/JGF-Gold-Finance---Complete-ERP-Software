<#
.SYNOPSIS
    Automated Daily Database Backup & Rotation for JGF Jewel Gold Finance.
.DESCRIPTION
    Dumps the PostgreSQL 'jgf_db' database into a timestamped, compressed backup file.
    Enforces a 30-day retention policy and logs the operation to the backup audit directory.
#>

param(
    [string]$BackupDir = "D:\JGF_Backups\Automated",
    [string]$Database = "jgf_db",
    [string]$Username = "postgres",
    [int]$RetentionDays = 30
)

# Fallback to C: if D: drive does not exist
if (-not (Test-Path (Split-Path $BackupDir -Qualifier))) {
    $BackupDir = "C:\JGF_Backups\Automated"
}

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = Join-Path $BackupDir "jgf_db_backup_$timestamp.dump"
$logFile = Join-Path $BackupDir "backup_audit.log"

$msg = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Starting automated backup of $Database to $backupFile..."
Add-Content -Path $logFile -Value $msg

# Locate pg_dump
$pgDumpPath = "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe"
if (-not (Test-Path $pgDumpPath)) {
    $pgDumpPath = (Get-ChildItem -Path "C:\Program Files\PostgreSQL" -Filter "pg_dump.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}

if (-not $pgDumpPath -or -not (Test-Path $pgDumpPath)) {
    $errMsg = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ERROR: pg_dump.exe could not be found!"
    Add-Content -Path $logFile -Value $errMsg
    exit 1
}

# Execute pg_dump
$env:PGPASSWORD = $env:PGPASSWORD ?? "postgres"
& $pgDumpPath -h localhost -p 5432 -U $Username -F c -b -v -f $backupFile $Database 2>&1 | Out-File (Join-Path $BackupDir "last_pg_dump.log")

if (Test-Path $backupFile) {
    $size = (Get-Item $backupFile).Length / 1MB
    $successMsg = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] SUCCESS: Backup created successfully ($([Math]::Round($size, 2)) MB)."
    Add-Content -Path $logFile -Value $successMsg

    # Clean up backups older than $RetentionDays
    Get-ChildItem -Path $BackupDir -Filter "*.dump" | Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-$RetentionDays) } | ForEach-Object {
        Remove-Item $_.FullName -Force
        Add-Content -Path $logFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Purged expired backup: $($_.Name)"
    }
} else {
    Add-Content -Path $logFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ERROR: Backup file was not generated."
}
