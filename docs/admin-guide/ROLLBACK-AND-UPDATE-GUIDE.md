# JGF Safe Update & Disaster Rollback Guide

This guide documents the procedures for performing safe updates and executing a clean rollback in the event of an update failure or operational issue.

---

## 1. Safe Update Architecture

JGF utilizes a **Zero-Data-Loss Safe Update Architecture**:
1. **Semantic Versioning**: The software enforces semantic versioning (`Major.Minor.Patch`). The `SchemaVersions` PostgreSQL table tracks all applied updates, timestamps, executing users, and backup references.
2. **Version Enforcement at Login**:
   - If a client PC is running an older binary than the database: **Login is strictly blocked** with the message *"Please update this PC to version X"*.
   - If a client PC is running a newer binary but the database has pending migrations: **Cashiers are blocked** (*"System update pending, contact the owner"*), while Owners/Managers are prompted to run the update wizard.
3. **Non-Skippable Pre-Update Backup**:
   - The system automatically creates a full encrypted snapshot (AES-256) of the database prior to applying any migration.
   - The backup is cryptographically verified (file size > 0 and schema test-decryption).
   - If the backup or its verification fails, **the update immediately terminates** with zero changes to the database.
4. **Precondition Safety Rules**:
   - Updates can only be triggered directly on **PC1 (Database Server)**.
   - Updates are blocked if the business day is open with unposted counter transactions.
   - Updates are blocked if any staff member is actively logged in on PC2.
5. **Post-Update Integrity Audit**:
   - Immediately following the database transaction, the system runs the Accounts Integrity Audit to confirm zero-rupee discrepancies across all ledgers.

---

## 2. Standard 4-Step Update Procedure

When upgrading from Version N to Version N+1:

### Step 1: Evening Day Close
1. Ensure all Counter 2 transactions are finished.
2. On PC1, navigate to **Accounts -> Day Close (F9)**.
3. Confirm physical cash balance matches system cash and click **Finalize Day Close**.

### Step 2: Launch Safe Update Wizard
1. On PC1 (Server), log in as **Owner**.
2. Navigate to **Settings -> Safe Update Wizard (Owner / Manager)**.
3. Click **1. Re-Check Safety Rules**. Confirm green status for Day Close, PC1 host, and PC2 session state.

### Step 3: Run Update & Pre-Update Backup
1. Enter your master backup encryption passphrase.
2. Click **2. Start Safe Update & Backup**.
3. The system will:
   - Generate and verify the encrypted snapshot in `Backups\PreUpdate\`.
   - Apply additive schema migrations inside a transactional boundary.
   - Run the post-migration Accounts Integrity Audit.
   - Record the upgrade in `SchemaVersions`.

### Step 4: Update PC2 (Client Counter)
1. Run the PC2 installer script: `Releases\v1.2.0\JGF-Setup-PC2-v1.2.0.bat`.
2. Launch JGF on PC2. Verify green connection indicator: `● DB: Connected (LAN PC1)`.
3. Check **Settings -> About JGF** on both PCs to confirm matching versions.

---

## 3. Disaster Rollback Procedure

If a software update causes unforeseen bugs, workflow interruptions, or incompatible data issues, follow these steps to execute a complete rollback:

### Step 1: Disconnect Client Counter
1. Shut down JGF on **PC2**.
2. Disconnect PC2 from the LAN network (unplug Ethernet cable) to prevent cashier writes during the rollback.

### Step 2: Locate Pre-Update Backup
1. On PC1, open `C:\JGF\Backups\PreUpdate\` (or the application's `Backups\PreUpdate\` directory).
2. Identify the backup file created immediately before the update, e.g.:
   `preupdate_backup_20261107_161500.enc`
3. Confirm file size is greater than 0 bytes.

### Step 3: Decrypt and Restore PostgreSQL Database
Open PowerShell as Administrator on PC1 and execute:

```powershell
$env:PGPASSWORD = "YourPostgresPassword"
$pgBin = "C:\Program Files\PostgreSQL\18\bin"

# 1. Terminate any active connections to jgf_db
& "$pgBin\psql.exe" -U postgres -h localhost -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'jgf_db' AND pid <> pg_backend_pid();"

# 2. Drop and recreate clean database
& "$pgBin\psql.exe" -U postgres -h localhost -c "DROP DATABASE IF EXISTS jgf_db;"
& "$pgBin\psql.exe" -U postgres -h localhost -c "CREATE DATABASE jgf_db;"

# 3. Restore backup snapshot
& "$pgBin\pg_restore.exe" -U postgres -h localhost -d jgf_db -v "C:\JGF\Backups\PreUpdate\preupdate_dump_latest.sql"
```

*(Note: If using encrypted `.enc` backup, use JGF's built-in **Settings -> Database Backup & Test Restore** screen to decrypt and restore).*

### Step 4: Reinstall Previous Application Version
1. On PC1, navigate to the previous release folder: `Releases\v1.1.0\`.
2. Run `JGF-Setup-PC1-v1.1.0.bat` to revert application binaries to version 1.1.0.
3. On PC2, navigate to `Releases\v1.1.0\` and run `JGF-Setup-PC2-v1.1.0.bat`.

### Step 5: Verification & Integrity Confirmation
1. Launch JGF on PC1.
2. Log in as Owner and open **Settings -> About JGF & System Version**.
3. Confirm:
   - Application Version matches Database Version (e.g. `1.1.0`).
   - The `SchemaVersions` history table displays the restored baseline version.
4. Navigate to **Accounts -> Accounts Integrity Audit (F9)** and click **Run Integrity Audit**.
5. Verify:
   - Total Voucher Debits == Credits (₹0.00 difference).
   - Loan Principal Outstanding == Ledger Head 1002.
6. Reconnect PC2 to the LAN and resume counter operations.
