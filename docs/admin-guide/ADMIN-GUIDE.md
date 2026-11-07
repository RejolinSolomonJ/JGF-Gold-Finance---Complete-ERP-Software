# JGF Jewel Gold Finance — System Administrator Guide

---

## 1. Network Topology & Roles

- **PC1 (Database Host & Server)**:
  - Static IP: e.g. `192.168.1.100` (Subnet: `255.255.255.0`, Gateway: Router IP)
  - Service: `postgresql-x64-18` on TCP port `5432`.
  - Windows Defender Firewall: Inbound rule `PostgreSQL JGF Counter Access` strictly scoped to PC2 LAN IP.
- **PC2 (Client Workstation)**:
  - Static IP: e.g. `192.168.1.101`.
  - Client application connects to PC1 via `Host=192.168.1.100;Port=5432;Database=jgf_db;Username=jgf_app;Password=<AppPassword>;SSL Mode=Prefer`.

---

## 2. PostgreSQL Configuration & Hardening

1. **Listen Addresses**:
   In `C:\Program Files\PostgreSQL\18\data\postgresql.conf`:
   ```conf
   listen_addresses = 'localhost,192.168.1.100'
   port = 5432
   max_connections = 50
   password_encryption = scram-sha-256
   ```

2. **Client Whitelist (`pg_hba.conf`)**:
   In `C:\Program Files\PostgreSQL\18\data\pg_hba.conf`:
   ```conf
   # Local Server Access
   host    jgf_db          jgf_app         127.0.0.1/32            scram-sha-256
   # PC2 Client Access
   host    jgf_db          jgf_app         192.168.1.101/32        scram-sha-256
   # Deny all other LAN traffic
   host    all             all             0.0.0.0/0               reject
   ```

3. **Least-Privilege App Role**:
   Run via `psql` or `scripts/security/harden-postgresql.ps1`:
   ```sql
   CREATE ROLE jgf_app WITH LOGIN PASSWORD 'YourStrongAppPassword';
   GRANT CONNECT ON DATABASE jgf_db TO jgf_app;
   GRANT USAGE ON SCHEMA public TO jgf_app;
   GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO jgf_app;
   GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO jgf_app;
   ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO jgf_app;
   ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO jgf_app;
   ```

---

## 3. Backup and Disaster Recovery

### Automated Daily Backup
- Scheduled task: `JGF_Daily_PostgreSQL_Backup` runs daily at `21:00` (9:00 PM).
- Script location: `packaging\pc1-server-installer\backup\run-automated-backup.ps1`.
- Primary storage: `C:\JGF\Backups\Daily`.
- Secondary storage: External USB drive (Drive `E:\` or `F:\` with BitLocker To Go).
- Retention: Backups older than 30 days are automatically pruned.

### Disaster Restore Procedure
If PC1 hardware suffers failure or data corruption:
*(For comprehensive step-by-step rollback of failed application updates, see [ROLLBACK-AND-UPDATE-GUIDE.md](file:///c:/Users/Rejolin%20Solomon%20J/Downloads/JGF%20Finance%20Software/docs/admin-guide/ROLLBACK-AND-UPDATE-GUIDE.md)).*
1. Provision replacement PC with Windows 11 and install PostgreSQL 18.
2. Open PowerShell as Administrator.
3. Run the automated restore verification script or command:
   ```powershell
   $env:PGPASSWORD = "YourPostgresPassword"
   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "DROP DATABASE IF EXISTS jgf_db;"
   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "CREATE DATABASE jgf_db;"
   & "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" -U postgres -h localhost -d jgf_db -v "C:\JGF\Backups\Daily\jgf_db_backup_latest.dump"
   ```
4. Verify table row counts and run the in-app Integrity Audit tool.

---

## 4. User Account & Permission Management

- **Creating New Staff Accounts**:
  1. Login as **Owner**.
  2. Navigate to **Settings -> User Management**.
  3. Click **Add User**, enter Full Name, Username, Temporary Password, and Role (`Manager` or `Cashier`).
  4. Ensure passwords adhere to complexity standards (letters + numbers, 8+ characters).
- **Deactivating Terminated Staff**:
  1. Select the user in the list and click **Deactivate Account**.
  2. The account is immediately disabled and cannot authenticate or decrypt customer records.

---

## 5. Interest Rate Scheme & Slab Rules Adjustment

Under the Tamil Nadu Pawn Brokers Act, rate structures can be adjusted per scheme:
1. Open **Settings -> Schemes & Rates**.
2. Select the Scheme to modify (e.g. `Standard Gold Loan 12%`).
3. Set:
   - **Base Interest Rate** (% per annum).
   - **Minimum Interest Period**: e.g., 7 days, 15 days, or 1 month minimum.
   - **Grace Period**: Number of days before penal or next slab triggers.
   - **Slab Rules**: e.g. 12% for months 1–3, 15% for months 4–6, 18% for months 7–12.
4. Changes apply prospectively to new loans or next interest calculations as configured.

