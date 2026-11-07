# JGF Jewel Gold Finance — System User Manual
**Tamil Nadu Pawn Broker & Jewel Loan Management System (தமிழ்நாடு நகைக்கடன் மேலாண்மை மென்பொருள்)**

---

## 1. Introduction & Architecture Overview

JGF is an enterprise jewel financing software designed strictly for Tamil Nadu pawnbroking businesses, compliant with the Tamil Nadu Pawn Brokers Act, 1943, and modern accounting standards.

The software runs in a **2-PC LAN architecture**:
- **PC1 (Server & Counter 1)**: Runs the PostgreSQL database engine, hosts primary application data, runs the automated backup scheduler, and serves as the Owner/Manager counter.
- **PC2 (Client & Counter 2)**: Runs the JGF client connected securely over LAN to PC1 for simultaneous receipt collection, customer onboarding, and pledging.

---

## 2. Role-Based Permissions & Responsibilities

| Role | Responsibilities | Permitted Modules |
| :--- | :--- | :--- |
| **Owner** | Full governance, rate revisions, financial accounting, auction settlements, data migration, and day-close sign-off. | All modules, System Configuration, Migration Tool, Audit Logs. |
| **Manager** | Customer onboarding, loan disbursal, scheme management, notices generation, re-pledge entries. | Dashboard, Customers, Loans, Receipts, Re-Pledge, Notices, Reports, Day Close. |
| **Cashier** | Day-to-day counter operations: receipt payments, part payments, jewel searches. | Dashboard, Customer Lookup, Loan Status, Receipts, Quick Reports. |

---

## 3. Module Workflows

### 3.1. First-Run Setup & Login
1. On first launch, if no database users exist, the **First-Run Setup Wizard** prompts for:
   - Owner Full Name & Username (e.g. `admin`)
   - Secure Password (minimum 8 characters)
   - Company/Firm Name, Registration No., Address, Mobile, and GSTIN.
2. Enter the credentials in the **Login Dialog** to enter the main dashboard.
3. If the terminal remains idle for 10 minutes, the **In-App Screen Lock** engages to protect sensitive financial records. Enter the account password to unlock.

---

### 3.2. Customer Onboarding (Member Detail - F1)
1. Press `F1` or click **Member Detail**.
2. Click **New Customer** (`Ctrl+N`).
3. Fill mandatory fields:
   - **Full Name** & **Care Of (Father / Husband Name)**.
   - **Mobile Number** (10 digits).
   - **Address** (Door No, Street, Village/Town, Pincode).
   - **Aadhaar Number** (12 digits, automatically masked and encrypted) or Photo ID reference.
4. Upload or snap customer webcam photo (optional).
5. Click **Save** (`Ctrl+S`). The system assigns a unique auto-generated customer code (e.g. `C0001`).

---

### 3.3. New Loan Entry (Pledging - F2)
1. Press `F2` or click **New Loan Entry**.
2. Search and select customer by Name, Code, or Mobile.
3. Select the **Pledge Scheme** (e.g. Standard 12% 1-Year or Monthly Slab Scheme).
4. Enter Jewel Items in the item grid:
   - **Jewel Type**: Chain, Ring, Bangle, Necklace, Coin, etc.
   - **Count**: Number of pieces.
   - **Gross Weight**: Total weight in grams (to 3 decimals).
   - **Deductions / Stones**: Stone and wax weight in grams.
   - **Net Weight**: Calculated automatically (`Gross - Deductions`).
   - **Purity**: 22 Karat (916) or 18 Karat.
5. Review the **Calculated Maximum Loan Value** based on the day's gold rate and statutory LTV (max 75%).
6. Enter **Sanctioned Loan Amount** (cannot exceed LTV limit).
7. Select payment mode: **Cash** or **Bank Transfer**.
8. Click **Save & Print Pawn Ticket** (`F12`).
   - Automatically records Voucher: `Debit: Jewel Loan Receivable (1002)`, `Credit: Cash on Hand (1001)`.
   - Prints Form F Pawn Ticket with customer details, jewel inventory, terms, and Tamil declarations.

---

### 3.4. Receipts & Repayments (F4)
1. Press `F4` or click **Receipt**.
2. Search loan by **GL / Loan Number** or Customer Name.
3. The system displays:
   - Pledge Date, Principal Amount, Days Elapsed.
   - Accrued Interest (calculated on exact day-basis according to scheme rules).
   - Postal / Notice charges (if applicable).
4. Select repayment type:
   - **Interest Only**: Clears accrued interest; principal remains intact.
   - **Part Payment**: Amount exceeds interest; balance reduces principal.
   - **Full Settlement**: Clears total interest + principal + charges; releases the pledge.
5. Enter payment mode (Cash, GPay/UPI, NEFT).
6. Click **Save & Print Receipt** (`F12`).
   - Automatically updates loan outstanding balance and accrues accounting vouchers.

---

### 3.5. Bank Re-Pledge (F5)
*(Owner & Manager only)*
1. Press `F5` to open the **Bank Re-Pledge Module**.
2. **New Bank Entry**: Select bank (e.g. SBI, Canara Bank, Muthoot), select customer loans to pledge, specify bank valuation, sanction amount, and interest rate.
   - System moves selected jewels from **Shop Safe Locker** to **Bank Vault** status.
   - Posts voucher: `Debit: Cash on Hand (1001)`, `Credit: Bank Repledge Loan Payable (2001)`.
3. **Bank Payment / Release**: Record monthly interest paid to bank or pay principal to release customer jewels back to shop safe locker.

---

### 3.6. Notices & Auction (F6)
*(Owner & Manager only)*
1. Press `F6` to inspect overdue loans.
2. Filter loans by overdue threshold (e.g., 6 months, 11 months, 12 months).
3. **Notice Printing**:
   - Select loans and click **Generate Notice Letters**.
   - Generates bilingual Tamil/English registered post notice letters with complete jewel descriptions and amounts due.
   - Marks notice dispatch date and registered postal tracking numbers.
4. **Auction Workflow**:
   - Loans passing mandatory notice expiry days become eligible for auction.
   - Requires explicit **Owner Approval**.
   - Record auction date, gazette notice reference, auctioneer details, and realized bid amounts.
   - System automatically calculates **Surplus** (payable to customer) or **Shortfall** (loss booked to P&L).

---

### 3.7. Reports & Daily Financials (F7 & F9)
1. **JL Reports (F7)**:
   - Loan Outstanding Register, Pledge Register, Closed Loan Register.
   - Stock Balance (Shop Locker vs. Bank Vault).
   - Interest Collection & Daily Cash Register.
   - One-click export to **Excel** (`.xlsx`) and **PDF**.
2. **Day Close & Integrity Audit (F9)**:
   - Reconcile physical cash drawer balance against system opening + collections - disbursements.
   - Run the **Integrity Check Audit**: Verifies that total voucher debits equal credits, and ledger 1002 matches loan outstanding.
   - Execute Day-Close lock to archive daily balances and trigger automated backup.

---

### 3.8. Legacy Data Migration Tool (Owner Only)
1. Navigate to **Settings -> Data Migration & Legacy Import**.
2. Select legacy CSV or Excel files for Customers, Loans & Jewels, Receipts, or Bank Pledges.
3. Enable **Dry Run** mode to test data validity, check for duplicate phone numbers, verify item net weights, and view error logs.
4. Click **Run Import** to commit records to the database.
5. Review the automated **Reconciliation Summary** ensuring zero rupee discrepancy against legacy totals.

