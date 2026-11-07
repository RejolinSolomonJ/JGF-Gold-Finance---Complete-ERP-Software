# UPS Hardware Specification & Safe-Shutdown Procedure (JGF 2-PC Production Setup)

This document outlines the mandatory power protection, UPS hardware guidelines, and safe shutdown procedures for JGF Jewel Gold Finance software deployed on a 2-PC LAN architecture.

---

## 1. UPS Hardware Requirements

### A. PC1 (Database Server & Master Counter)
Because PC1 hosts the central PostgreSQL database (`jgf_db`) for both counters, uninterrupted clean power is essential to prevent data corruption during power grid fluctuations.

- **Recommended UPS Model**: **APC Smart-UPS SMC1000I-2UC (1000VA / 600W)** or **Microtek Line-Interactive 1000VA (Pure Sine Wave)**.
- **Topology**: Line-Interactive with Automatic Voltage Regulation (AVR) and Pure Sine Wave output.
- **Runtime**: Minimum 20 to 30 minutes under typical load (PC1 tower, monitor, receipt printer, thermal barcode scanner, and 8-port Cat6 gigabit network switch).
- **USB Auto-Shutdown Signaling**:
  - Connect the USB signaling cable from the UPS to PC1.
  - Install the UPS power management software (e.g. APC PowerChute Business Edition).
  - Configure the policy:
    - *When battery reaches 20% remaining capacity or 10 minutes on battery, trigger Windows safe shutdown.*

### B. PC2 (Counter 2 Client)
- **Recommended UPS Model**: **APC Back-UPS BX600C-IN (600VA / 360W)** or **Luminous 600VA**.
- **Runtime**: 10 to 15 minutes to allow the cashier to complete the active customer transaction and close the application.

---

## 2. Power Loss Resilience & Transaction Safety

JGF incorporates enterprise-grade reliability patterns designed specifically for power cuts:

1. **PostgreSQL WAL (Write-Ahead Logging)**:
   - PostgreSQL logs all changes to disk before writing data pages.
   - If PC1 abruptly loses power while an operation is in flight, upon reboot PostgreSQL executes crash recovery by replaying uncheckpointed WAL records.
2. **Atomic All-or-Nothing Transactions**:
   - Every loan issuance, receipt collection, bank re-pledge, and day close executes inside an explicit database transaction.
   - If power fails halfway through writing a receipt voucher, PostgreSQL rolls back the entire transaction. **No half-saved vouchers or orphaned receipt lines can ever exist.**
3. **Automatic Reconnection & Retry**:
   - PC2's connection to PC1 utilizes `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: 5 seconds)`.
   - If the network switch reboots or a transient LAN hiccup occurs, PC2 transparently retries the query without crashing or freezing the cashier's screen.
4. **Optimistic Concurrency Control**:
   - Both `Loan` and `Customer` entities have `[ConcurrencyCheck] public int ConcurrencyVersion`.
   - If Cashier 1 (PC1) and Cashier 2 (PC2) open the same loan record simultaneously and Cashier 1 saves a receipt first, Cashier 2 will see:
     > *"This loan was modified by another user on Counter 1. Please reload the loan before saving."*
   - This prevents either cashier from overwriting payments, balances, or status updates.

---

## 3. Safe Daily Opening & Shutdown Procedure

### Morning Opening (9:00 AM)
1. Switch on the main UPS power supply.
2. Power on **PC1 (Server)** first. Wait 60 seconds until Windows boots and the PostgreSQL service starts.
3. Power on **PC2 (Counter 2)**.
4. On PC1, log in as **Owner** or **Manager**:
   - Set the Daily Gold Rate under **Settings -> Daily Gold Rate**.
   - Check the bottom status bar: verify `● DB: Connected (LAN PC1)` and `Backup: Up to date`.
5. On PC2, launch JGF: verify green database connection indicator.

### Evening Shutdown (8:30 PM - 9:00 PM)
1. **Both Cashiers**: Ensure all customer transactions are saved.
2. **Owner / Manager**:
   - Run the **Day-End Close** under **Accounts -> Day-End Close**.
   - Verify that Cash on Hand matches physical drawer cash.
   - Close the business day (locks entries for that calendar date).
3. **Automated Backup**:
   - The scheduled backup will automatically run at 9:00 PM and dump `jgf_db` to `D:\JGF_Backups\Automated`.
   - Ensure the external encrypted backup USB drive is plugged in.
4. Close the JGF application on PC2, then shut down PC2.
5. Close the JGF application on PC1, then perform a standard Windows **Start -> Shut down** on PC1.
6. Switch off UPS only after PC1 has completely turned off.

