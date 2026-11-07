# BitLocker & Physical Security Checklist (JGF Gold-Loan Finance)

This checklist provides the mandatory security hardening steps for deploying JGF Jewel Gold Finance software in a physical shop counter environment with a 2-PC setup.

---

## 1. BitLocker Drive Encryption (PC1 & PC2)

### A. Pre-requisites
- [ ] Motherboard TPM (Trusted Platform Module) 2.0 must be enabled in UEFI/BIOS.
- [ ] Windows 10/11 Pro or Enterprise edition installed.
- [ ] UEFI Secure Boot enabled.

### B. OS Drive Encryption (Drive C:)
1. Open Windows Search and type **"Manage BitLocker"**.
2. Click **"Turn on BitLocker"** for Operating System drive `C:`.
3. Choose **"Automatically unlock this drive on this computer"** (using TPM).
4. **CRITICAL - Recovery Key Backup**:
   - Select **"Print the recovery key"** and **"Save to a file"** (save to a dedicated secure USB drive).
   - Print two physical paper copies:
     - Copy 1: Kept inside the shop's fireproof iron safe / locker alongside pledge packets.
     - Copy 2: Stored in the Owner's home safe.
   - *Never leave BitLocker recovery keys stored on the PC desktop or shared drives.*
5. Select **"Encrypt used disk space only"** (for new PCs) or **"Encrypt entire drive"** (for in-use PCs).
6. Encryption Mode: Choose **"New encryption mode" (XTS-AES 256-bit)**.
7. Run the BitLocker system check and restart the machine.

---

## 2. External Backup USB Encryption (BitLocker To Go)

For daily offline backups:
1. Insert the primary external USB backup drive into PC1.
2. Open **"Manage BitLocker"** -> Under **Removable data drives**, click **"Turn on BitLocker"**.
3. Select **"Use a password to unlock the drive"** (enter a strong 16+ character password known only to the Owner).
4. Save the 48-digit recovery key in the physical shop safe.
5. Enable **"Automatically unlock on this computer"** only for PC1.
6. BitLocker ensures that even if an external backup drive is stolen or lost during transport, customer loan data and KYC records remain encrypted and inaccessible.

---

## 3. Physical Shop & Workstation Hardening

- [ ] **PC1 Placement**: Place PC1 (Database Server + Counter 1) in a secure, protected area behind the counter or inside a ventilated lockable cabinet.
- [ ] **BIOS/UEFI Password**: Set an Administrator password in UEFI/BIOS on both PC1 and PC2 to prevent unauthorized booting from USB sticks or Linux Live CDs.
- [ ] **USB Port Security**: Disable boot from USB in BIOS boot order settings.
- [ ] **Screen Privacy Filters**: Install 3M privacy filters on PC1 and PC2 monitors so customers standing at the counter cannot view ledger accounts, cash balances, or other customer loans from angles.
- [ ] **Physical Network Cabling**: Connect PC1 and PC2 directly via a dedicated Cat6 Ethernet switch or direct patch cable rather than Wi-Fi to prevent wireless packet interception and interference.

---

## 4. In-App Security Settings

- [ ] **10-Minute Screen Lock Timeout**:
  - JGF automatically locks the screen after 10 minutes of cashier inactivity.
  - Cashiers must enter their account password to unlock the screen.
- [ ] **Role-Based Access Control**:
  - Owner role: Full access including Audit Trail, Data Migration, Financial Reports, and Settings.
  - Manager role: Loan issue, payments, re-pledge, and customer master.
  - Cashier role: Loan issue and receipt entry only; blocked from ledger heads, audit logs, and system settings.
- [ ] **Daily Password Discipline**:
  - No shared logins: Cashier 1 must use account `cashier1`, Cashier 2 must use `cashier2`.
  - Passwords changed every 90 days.

