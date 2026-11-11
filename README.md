# Jeba Gold Finance (JGF) — Jewel Loan Financial Software
### நகை கடன் நிதி மேலாண்மை மென்பொருள்

[![.NET 8](https://img.shields.io/badge/.NET-8.0_WPF-blue.svg)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16%2B%20%2F%2018-336791.svg)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/Compliance-TN_Pawnbrokers_Act-gold.svg)]()
[![Tests](https://img.shields.io/badge/Tests-92%2F92_Passing-brightgreen.svg)]()

**Jeba Gold Finance (JGF)** is an enterprise-grade desktop gold loan and pawnbroking management system designed specifically for jewel loan establishments and non-banking finance operations in Tamil Nadu, India. Fully compliant with statutory pawnbroking regulations, the system delivers high-speed counter disbursals, month-by-month interest calculations, customer balance tracking, bank re-pledge workflows, overdue notices, and automated day-close financial accounting.

---

## 🏛️ Key Features

### 1. New Jewel Loan Entry (புதிய நகை கடன் பதிவு)
- **High-Speed Counter Entry**: Disburse loans in under 60 seconds with full keyboard navigation (`Tab` / `Enter` flow).
- **Webcam & Scale Integration**: Direct capture of customer KYC photo and jewel scale weight photos directly into the pawn record.
- **LTV Ceiling & Gold Rate Valuation**: Dynamic Loan-to-Value (LTV) limits based on daily 22K/24K gold rates with Owner override controls.
- **Automated Packet & Barcode**: Gapless FY number series (`GL/2024-25/XXXX`) and auto packet numbering (`PKT-AUTO`).

### 2. Receipts & Pay to Close (ரசீது & கடன் அடைப்பு)
- **3 Payment Modes**:
  1. *Interest Only* — Collects accrued interest to date; principal remains intact.
  2. *Part Payment* — Reduces principal balance with live recalculation.
  3. *Full Closure & Release* — Settles principal and interest to 0.00 and generates the Jewel Release Slip.
- **⚡ "Pay to Close Today" Golden Card**: Instant penny-accurate breakdown of the exact amount the customer must pay *today* to completely close their loan (`Remaining Principal + Accrued Interest to Date`).
- **Live Post-Payment Balance Banner**: Clear visual indicator showing the exact remaining balance after the transaction.
- **Automatic Closure Prompt**: When a customer's payment covers the full balance, the software automatically prompts to close the loan and release the collateral.

### 3. Customer Balance Tracker (வாடிக்கையாளர் மீதி கண்காணிப்பு)
- **Unified Customer Portfolio**: Search by customer code, name, or phone number to view all their active, past, and overdue loans in one place.
- **4 Real-Time Executive KPI Cards**:
  - *Active Loans Count & Total Pledged Gold Weight*
  - *Cumulative Disbursed Principal*
  - *Current Outstanding Principal & Accrued Interest*
  - *Total to Pay to Close All Loans (அனைத்து கடன் அடைக்க)*
- **1-Click Settlement**: Click `⚡ Settle / Pay` on any loan in the customer's portfolio to jump straight into the receipt entry screen with that loan loaded.
- **Statement & Ledger**: Print customer statements or export running balance ledgers to CSV.

### 4. Overdue, Notices & Auction Recovery (அறிவிப்புகள் மற்றும் ஏல நடவடிக்கை)
- **Statutory Pawnbroker Compliance**: Enforces statutory grace periods and mandatory 15-day pre-notice buffer before auction eligibility.
- **3-Tier Notice Workflow**:
  - *Friendly Reminder Notice (நட்பு நினைவூட்டல்)*
  - *Final Demand Notice (இறுதி எச்சரிக்கை)*
  - *Form 10 Statutory Notice (சட்டபூர்வ ஏல அறிவிப்பு)*
- **Auction Scheduling & Surplus Recovery**: Full ledger tracking of auction proceeds, interest recovery, and surplus returns to borrowers.

### 5. Bank Re-Pledge Management (வங்கி மறு அடகு)
- Manage shop loans re-pledged to commercial / cooperative banks.
- Track bank packet numbers, interest rates, sanctioned limits, and maturity schedules.
- Jewel stock custody tracking (Shop Locker vs. Bank Custody).

### 6. Day Close & Double-Entry Accounting (தினசரி கணக்கு முடிவு)
- Complete internal general ledger adhering to standard double-entry principles (Cash Book, Day Book, Trial Balance, P&L, Balance Sheet).
- Automated daily database backup (`pg_dump`) with AES-256 encryption and LAN synchronization during Day Close.

---

## 🏗️ Architecture & Technology Stack

```
JGF Solution
├── src/
│   ├── JGF.Domain/          # Pure entity models, enums, domain rules (No dependencies)
│   ├── JGF.Application/     # CQRS/Service interfaces, DTOs, Interest Calculator, Reports
│   ├── JGF.Infrastructure/  # EF Core 9, PostgreSQL provider, BCrypt, Serilog, Services
│   └── JGF.Desktop/         # WPF MVVM UI, XAML Controls, Dialogs, Printers, Helpers
├── tests/
│   └── JGF.Tests/           # 92 unit and integration tests (xUnit, FluentAssertions, Moq)
└── packaging/
    └── installer.iss        # Inno Setup 6 dual-mode script (Server PC1 / Client PC2)
```

- **Runtime**: .NET 8.0 (Windows Desktop SDK)
- **UI Framework**: WPF (Windows Presentation Foundation) with modern styling and Noto Sans Tamil typography
- **Database**: PostgreSQL 16+ / 18 via Entity Framework Core 9 (Npgsql)
- **Security**: BCrypt password hashing, AES-256 backup encryption, Role-Based Access Control (Owner / Staff)
- **Auditing**: Immutable PostgreSQL `jsonb` audit logs on all data mutations

---

## 🚀 Getting Started

### Prerequisites
1. **Windows 10 / 11** (64-bit)
2. **.NET 8.0 SDK** (for development) or .NET 8.0 Desktop Runtime (for running binary)
3. **PostgreSQL 16+** installed locally or reachable over the local network (LAN)

### Quick Run from Source
```powershell
# 1. Clone the repository
git clone https://github.com/rejolinsolomon/JGF-Finance-Software.git
cd "JGF Finance Software"

# 2. Restore and Build
dotnet build src/JGF.Desktop/JGF.Desktop.csproj

# 3. Run Automated Tests
dotnet test tests/JGF.Tests/JGF.Tests.csproj

# 4. Launch Desktop Application
dotnet run --project src/JGF.Desktop/JGF.Desktop.csproj
```

### Database Connection Setup
The database connection string is configured in `appsettings.json` (or overridden in `C:\ProgramData\JGF\appsettings.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=jgf_db;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Prefer;Timeout=5;Command Timeout=15;"
  }
}
```

---

## 📦 Building Installers

Installers are generated using [Inno Setup 6](https://jrsoftware.org/isdl.php):

```powershell
# 1. Publish self-contained Windows x64 binaries
dotnet publish src/JGF.Desktop/JGF.Desktop.csproj -c Release -r win-x64 --self-contained true -o src/JGF.Desktop/bin/Release/net8.0-windows/publish/win-x64

# 2. Compile Server (PC1) Installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMODE=Server /DAPP_VERSION=1.2.0 packaging/installer.iss

# 3. Compile Client (PC2) Installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMODE=Client /DAPP_VERSION=1.2.0 packaging/installer.iss
```
Installers will be generated in `Releases/`:
- `JGF-Server-Setup-1.2.0.exe` (Primary machine with PostgreSQL)
- `JGF-Client-Setup-1.2.0.exe` (Secondary counter machine connecting over LAN)

---

## 📄 License & Ownership
Copyright © 2024–2026 **Jeba Gold Finance (JGF)**. All Rights Reserved.  
Proprietary software developed for Jeba Gold Finance operations.
