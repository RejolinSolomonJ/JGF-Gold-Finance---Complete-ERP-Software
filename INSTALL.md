# JGF Jewel Gold Finance — Production Installation & Deployment Guide

This document describes how to build, deploy, and verify **Jeba Gold Finance (JGF)** for a 2-PC setup:
- **PC1 (Server + Counter 1)**: Runs the local PostgreSQL 15+ database and the JGF application.
- **PC2 (Client + Counter 2)**: Cashier workstation communicating with PC1 over the local shop network (LAN).

---

## 1. Build Machine Prerequisites

To compile the application and build the setup installers, the build machine requires:

| Tool | Minimum Version | Installation Command / Link |
| :--- | :--- | :--- |
| **Windows OS** | Windows 10/11 (x64) | Built-in |
| **.NET SDK** | .NET 8.0 SDK | `winget install Microsoft.DotNet.SDK.8` or [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Inno Setup** | Inno Setup 6.x | `winget install JRSoftware.InnoSetup` or [jrsoftware.org](https://jrsoftware.org/isdl.php) |
| **PowerShell** | PowerShell 5.1+ / 7+ | Built-in |
| **Git** | Any modern version | `winget install Git.Git` |

---

## 2. Building the Installers

Open PowerShell in the project root directory and run:

```powershell
# Run full automated test suite, publish win-x64 binaries, and build both installers:
.\build-installer.ps1

# Optional: To build without re-running tests:
.\build-installer.ps1 -SkipTests
```

The script builds self-contained installers in the `Releases\` folder:
- `Releases\JGF-Server-Setup-1.2.0.exe` (~62 MB) — For **PC1 (Server)**
- `Releases\JGF-Client-Setup-1.2.0.exe` (~62 MB) — For **PC2 (Client)**

---

## 3. Network Architecture & Planning

Before installing, configure your shop's Wi-Fi router / network switch with static IP addresses or DHCP reservations:

```
┌─────────────────────────────────┐       ┌─────────────────────────────────┐
│           PC1 (SERVER)          │       │           PC2 (CLIENT)          │
│   IP: 192.168.1.100 (Static)    │◄─────►│   IP: 192.168.1.101 (Static)    │
│  - PostgreSQL 5432 (Hardened)   │  LAN  │  - JGF Desktop App (Counter 2)  │
│  - JGF Desktop App (Counter 1)  │       │                                 │
│  - Automated 9:00 PM Backups    │       │                                 │
└─────────────────────────────────┘       └─────────────────────────────────┘
```

---

## 4. Installation Steps for PC1 (Server)

1. **Pre-requisite (PostgreSQL Database Engine)**:
   - Ensure PostgreSQL (v15+) is installed. If not already installed, download PostgreSQL from [postgresql.org/download/windows](https://www.postgresql.org/download/windows/) and install with default port `5432` and superuser password `postgres` (or place `postgresql-installer.exe` in `packaging\prerequisites\` before building).
2. **Run Installer**:
   - Right-click `JGF-Server-Setup-1.2.0.exe` and select **Run as Administrator**.
   - Choose destination folder (default: `C:\Program Files\JGF`).
3. **Server Configuration Page**:
   - **PostgreSQL Master Password**: Enter the `postgres` password (default: `postgres`).
   - **PC2 Client LAN IP**: Enter PC2's LAN IP address (default: `192.168.1.101`).
   - **App User Password**: Enter a secure password for `jgf_app` (default: `JgfSecure_AppPassword2026!`).
4. **Automated Setup Execution**:
   The installer automatically:
   - Configures `postgresql.conf` (`listen_addresses = '*'`) and `pg_hba.conf` to whitelist only PC1 and PC2.
   - Provisions database `jgf_db` and role `jgf_app`.
   - Creates Windows Firewall inbound rule for port 5432 for PC2.
   - Registers daily 9:00 PM backup task in Windows Task Scheduler (`JGF-Daily-Automated-Backup`).
   - Installs the Tamil font (`Noto Sans Tamil`) into Windows Fonts.
   - Writes configuration to durable storage `C:\ProgramData\JGF\appsettings.json`.
5. **First-Run Setup Wizard**:
   - On completion, check "Launch Jeba Gold Finance".
   - The First-Run Wizard will appear:
     1. Click **⚡ Test Connection** to verify database connectivity.
     2. Set Owner admin username, full name, and password.
     3. Fill in firm details (Name, Registration number, Address, Phone, GST).
     4. Click **Complete Setup & Initialize**.

---

## 5. Installation Steps for PC2 (Client / Counter 2)

1. **Ensure PC1 is Running**:
   - Make sure PC1 is turned on, logged in, and connected to the same shop LAN network.
2. **Run Installer**:
   - Copy `JGF-Client-Setup-1.2.0.exe` to PC2.
   - Right-click `JGF-Client-Setup-1.2.0.exe` and select **Run as Administrator**.
3. **Client Configuration Page**:
   - **PC1 Server LAN IP**: Enter the IP address of PC1 (e.g., `192.168.1.100`).
   - **App User Password**: Enter the same password configured on PC1 (default: `JgfSecure_AppPassword2026!`).
4. **Finish & Launch**:
   - The installer installs the application to `C:\Program Files\JGF`, registers the Tamil font, creates Desktop shortcuts, and sets `C:\ProgramData\JGF\appsettings.json`.
   - Launch JGF: Log in with cashier or manager credentials. Both PC1 and PC2 can issue loans, print receipts, and pledge items simultaneously.

---

## 6. Testing the Installers on a Clean Virtual Machine

You can test both installers on a clean virtual machine (Windows Sandbox, Hyper-V, or VirtualBox):

### Method A: Windows Sandbox (Fastest Smoke Test)
1. In Windows PowerShell (Admin), enable Windows Sandbox if not already enabled:
   ```powershell
   Enable-WindowsOptionalFeature -FeatureName "Containers-DisposableClientVM" -All -Online
   ```
2. Open Windows Sandbox.
3. Copy `JGF-Server-Setup-1.2.0.exe` into the Sandbox window.
4. Run the installer, proceed through the setup pages, and verify:
   - App installs to `C:\Program Files\JGF`.
   - Desktop and Start Menu shortcuts appear with the gold JGF logo icon.
   - Uninstaller is listed in Settings > Installed Apps.
   - Running the uninstaller leaves `C:\ProgramData\JGF` (logs and backups) untouched.

### Method B: 2-VM Hyper-V / VirtualBox Concurrency Test
1. Set up two Windows 10/11 VMs connected to an Internal / Bridged virtual switch.
   - VM1: Static IP `192.168.1.100` (Name: `JGF-SERVER`)
   - VM2: Static IP `192.168.1.101` (Name: `JGF-CLIENT`)
2. On VM1, install PostgreSQL 16 and run `JGF-Server-Setup-1.2.0.exe`. Complete First-Run Wizard.
3. On VM2, run `JGF-Client-Setup-1.2.0.exe` entering `192.168.1.100`.
4. Open JGF on both VMs simultaneously:
   - Create a loan on VM1 and immediately search/view it on VM2.
   - Pay a receipt on VM2 and verify day book totals update in real-time on VM1.

---

## 7. Data Storage & Safety Rules

- **Application Directory**: `C:\Program Files\JGF\`
- **Data, Logs & Backups**: `C:\ProgramData\JGF\`
  - Logs: `C:\ProgramData\JGF\Logs\jgf-YYYYMMDD.log`
  - Backups: `C:\ProgramData\JGF\Backups\`
  - Config: `C:\ProgramData\JGF\appsettings.json`
- **Uninstall Protection**: Uninstalling the application **never** deletes `C:\ProgramData\JGF` or database records.

---

## 8. Troubleshooting

### Issue 1: Connection Failed: 3D000 (Database does not exist)
- **Fix**: The application and installer automatically connect to PostgreSQL and issue `CREATE DATABASE "jgf_db"`. If this error appears in Settings, click **⚡ Test Connection** in Settings > Database Connection Settings. The wizard will automatically create the database and connect.

### Issue 2: PostgreSQL not reachable (Connection Timeout / 28P01)
- Verify PostgreSQL service is running on PC1:
  ```powershell
  Get-Service *postgres*
  ```
- From PC2, test network reachability to PC1:
  ```powershell
  Test-NetConnection -ComputerName 192.168.1.100 -Port 5432
  ```
- If port 5432 fails, check that Windows Firewall on PC1 has allowed incoming connections for port 5432:
  ```powershell
  Get-NetFirewallRule -DisplayName "JGF-PostgreSQL-LAN"
  ```
- Verify `C:\Program Files\PostgreSQL\<version>\data\pg_hba.conf` contains an entry allowing PC2 IP:
  ```
  host    jgf_db     jgf_app     192.168.1.101/32     scram-sha-256
  ```

### Issue 3: Tamil Text shows boxes or question marks
- The installer automatically installs `NotoSansTamil-Regular.ttf` to Windows Fonts (`C:\Windows\Fonts`).
- If Tamil font is missing, open `C:\Program Files\JGF\Assets\Fonts\NotoSansTamil-Regular.ttf`, right-click and click **Install for all users**.
- Restart the application.

### Issue 4: Automated Backup Verification
- Check Windows Task Scheduler on PC1:
  ```powershell
  Get-ScheduledTask -TaskName "JGF-Daily-Automated-Backup"
  ```
- To run an instant manual backup test:
  ```powershell
  & "C:\Program Files\JGF\scripts\backup\run-automated-backup.ps1"
  ```
- Inspect backup log at `C:\ProgramData\JGF\Backups\backup_audit.log`.
