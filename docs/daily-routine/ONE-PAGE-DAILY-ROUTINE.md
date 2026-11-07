# JGF Jewel Gold Finance — Daily Operational Routine
*(Print this page and keep at the cashier and manager counter)*

---

## 🌅 Morning Opening Routine (09:00 AM)

1. **Power Sequence**:
   - Turn on UPS main power.
   - Boot **PC1 (Server PC)** first and wait for Windows desktop to load.
   - Boot **PC2 (Counter Client PC)**.
2. **Database Verification**:
   - Launch **JGF Jewel Loan System** on PC1.
   - Verify green indicator in status bar: `● DB: Connected (LAN PC1)`.
   - Launch **JGF** on PC2 and confirm connected status.
3. **Daily Gold Rate Entry**:
   - Owner or Manager opens **Settings -> Daily Gold Rate**.
   - Enter today’s 22K (916) market gold rate per gram and standard lending rate per gram. Click **Save**.
4. **Physical Cash Drawer Opening**:
   - Open cash drawer and count starting cash balance.
   - Confirm it matches the previous evening's closing cash balance on the **Dashboard**.

---

## 💼 Counter Operations (09:30 AM – 07:30 PM)

| Action | Shortcut / Path | Key Checkpoints |
| :--- | :--- | :--- |
| **New Customer** | `F1` -> `Ctrl+N` | Verify Aadhaar / ID, mobile number, care-of name, and address. |
| **Pledge Loan** | `F2` | Accurately weigh gross and stone deductions. Net weight auto-computes. Verify customer signature on Form F Pawn Ticket. Place tagged jewel bag in safe locker. |
| **Receipt Payment** | `F4` | Search GL number. Reconcile interest and part-payment breakdown. Issue signed receipt printout to customer. |
| **Loan Release** | `F4` (Full Close) | Verify full payment received. Verify jewel bag number with locker register. Have customer inspect and sign jewel return acknowledgement. |
| **Lock Screen** | `Ctrl+L` or Auto | Lock terminal whenever stepping away from the counter for lunch or breaks. |

---

## 🌙 Evening Closing Routine (07:30 PM – 08:00 PM)

1. **Cash Drawer Reconciliation**:
   - Count physical cash: notes (₹500, ₹200, ₹100, ₹50, ₹20, ₹10) and coins.
   - Open **Accounts -> Day Close (F9)**.
   - Compare physical cash count against System Calculated Cash Balance.
   - Any difference must be verified against unposted receipts or expenses before proceeding.
2. **Safe Locker Physical Audit**:
   - Reconcile jewel bags placed in locker against today's New Pledges report.
   - Confirm released jewel packets have been handed over to customers and signed.
   - Double-lock physical safe locker.
3. **Run Financial Integrity Check**:
   - Click **Run Integrity Audit** on Day Close screen.
   - Confirm `Integrity Status: PASSED (Zero Discrepancies)`.
   - Click **Finalize Day Close**.
4. **Automated Backup Verification**:
   - Ensure the automated daily backup executes at 09:00 PM (or trigger manual backup via **Settings -> Database Backup**).
   - Verify backup log says `Status: SUCCESS` and note backup file on external USB drive.
5. **System Shutdown**:
   - Exit JGF on PC2 and shut down PC2.
   - Exit JGF on PC1 and safely shut down PC1 via Windows Start Menu.
   - Turn off UPS power after PCs have completely powered down.

