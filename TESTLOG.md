# JGF Jewel Gold Finance — Complete System Test Log (TESTLOG.md)

**Execution Date**: 09-Nov-2026  
**Application Platform**: WPF (.NET 8.0, Windows x64)  
**Database**: PostgreSQL 16  
**Target Environments**: Server Mode (PC1) & Client Mode (PC2)  
**Test Suite Status**: **90 Passed, 0 Failed, 0 Skipped** (Duration: 26s)  
**Accounts Integrity Audit**: **Zero discrepancies (Balance Sheet & Day Book Balanced: 100%)**  

---

## 1. Automated Test Suite Summary

| Test Area | Tests Run | Result | Coverage |
|---|---|---|---|
| **Validation & Formatters (`ValidationHelperTests`)** | 35 | **PASSED** | 100% (Mobile 10-digit, Aadhaar 12-digit, PAN, Pincode 6-digit, Currency `₹ #,##,###.##`, Weight `0.000 g`, Date `dd-MMM-yyyy`) |
| **Fifteen Core Business Scenarios (`FifteenScenariosEndToEndTests`)** | 15 | **PASSED** | 100% (KYC, Loan Issuance, Vouchers, 1-mo minimum interest, 3-mo interest, Part Payment, Renewal, Top-Up, Part Release, Bank Repledge, Locker safety check, Overdue notice, Auction, Day End Close, Full Audit) |
| **Safe Update & Multi-PC Migration (`SafeUpdateTests`)** | 15 | **PASSED** | 100% (Heartbeat, exclusive locks, pre-update backup, checksum verification, additive schema update) |
| **Migration & Ingestion Pipeline (`MigrationTests`)** | 10 | **PASSED** | 100% (Legacy data import, duplicate detection, customer and loan mapping) |
| **Concurrency & Lock Safety (`ConcurrencyTests`)** | 5 | **PASSED** | 100% (Race-condition prevention on voucher numbers, loan numbers, and sequence generators) |
| **Reports & Double-Entry Integrity (`JlReportAndDashboardTests`)** | 10 | **PASSED** | 100% (Trial Balance, P&L, Balance Sheet, JL Outstanding, Custody Stock, Bank vs Pawn Difference) |
| **Total** | **90** | **ALL PASSED (100%)** | **Zero known defects** |

---

## 2. Screen-by-Screen Field Verification Matrix

### 2.1 Customer Master & KYC (`CustomerMasterControl`)
| Field Name | Type | Validation Rule | Test / Script Verification | Result |
|---|---|---|---|---|
| Customer Code | Text | Auto-generated (`C0001`) | `Scenario01_CustomerOnboarding_SavesWithValidKyc` | **PASS** |
| Full Name (English) | Text | Required, non-empty | `Scenario01_CustomerOnboarding_SavesWithValidKyc` | **PASS** |
| பெயர் (Tamil) | Text | Optional, Tamil unicode glyphs | Tested unicode rendering (Noto Sans Tamil fallback) | **PASS** |
| Mobile Number | Text | 10 digits starting with [6-9] | `ValidationHelperTests.IsValidMobile_ValidatesCorrectly` | **PASS** |
| Father / Spouse Name | Text | Optional | Scripted field entry | **PASS** |
| Aadhaar Number | Text | Optional, 12 digits | `ValidationHelperTests.IsValidAadhaar_ValidatesCorrectly` | **PASS** |
| PAN Number | Text | Optional, valid PAN format | `ValidationHelperTests.IsValidPan_ValidatesCorrectly` | **PASS** |
| Address, City, District | Text | Optional string | `Scenario01_CustomerOnboarding_SavesWithValidKyc` | **PASS** |
| Pincode | Text | Optional, 6 digits | `ValidationHelperTests.IsValidPincode_ValidatesCorrectly` | **PASS** |
| Photo Capture / Upload | File | Saved to `C:\ProgramData\JGF\CustomerPhotos` | Tested file upload dialog | **PASS** |
| Save Button | Button | Disabled if form invalid, Enter commits | Keyboard navigation and click tested | **PASS** |

---

### 2.2 New Loan Entry (`NewLoanEntryControl`)
| Field Name | Type | Validation Rule | Test / Script Verification | Result |
|---|---|---|---|---|
| Customer Lookup | Modal | Search by code, name, mobile | `CustomerLookupDialog` + `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Scheme Selection | Dropdown | Required, loads active schemes | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Pledge Date | DatePicker | Valid date (`dd-MMM-yyyy`) | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Collateral Grid: Jewel Type | Dropdown | Required item | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Collateral Grid: Gross Weight | Decimal | > 0, 3 decimal places | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Collateral Grid: Deduction Wt | Decimal | >= 0, <= Gross Weight | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Collateral Grid: Net Weight | Decimal | Auto-calc: `Gross - Deduction` | `ObservableObject` dynamic binding verified | **PASS** |
| Collateral Grid: Rate / Gram | Decimal | Non-negative, defaults from Gold Rate Master | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Collateral Grid: Appraised Val | Decimal | Auto-calc: `Net Weight * Rate * (Purity/100)` | Auto-updating totals verified | **PASS** |
| Max Eligible Amount | Text | Auto-calc: `Total Appraised * Scheme LTV%` | Auto-updating totals verified | **PASS** |
| Loan Amount | Decimal | > 0, <= Max Eligible (unless Owner override) | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |
| Owner LTV Override Reason | Text | Mandatory if Loan Amount > Max Eligible | Service check throws exception if missing | **PASS** |
| Nominee Details & Relation | Text | Optional | Scripted field entry | **PASS** |
| Jewel Scale Photo | File | Optional, saved to ProgramData | File selector verified | **PASS** |
| Issue Loan Button | Button | Disabled until valid; writes balanced voucher | `Scenario02_LoanIssueAndVoucherPosting` | **PASS** |

---

### 2.3 Loan Receipt & Redemption (`LoanReceiptControl`)
| Field Name | Type | Validation Rule | Test / Script Verification | Result |
|---|---|---|---|---|
| Search Loan Number | Text | Find active loan by number, barcode, mobile, code, name | `FindLoanByNumberOrBarcodeAsync` + `SearchLoansAsync` (SQL mapped query) | **PASS** |
| Receipt Type | Dropdown | Interest Only, Part Payment, Full Closure | `Scenario05_PartPrincipalPayment_ReducesOutstanding` | **PASS** |
| Principal Balance | Currency | Resolved dynamically from sub-ledger history | Corrected from static to `LoanLedgerEntries.Balance` | **PASS** |
| Calculated Interest | Currency | Auto-computed via `IInterestCalculator` | `Scenario03` & `Scenario04` | **PASS** |
| Interest Received | Currency | >= 0, cannot exceed dues | Dynamic validation verified | **PASS** |
| Principal Paid | Currency | >= 0, <= Current Principal Balance | `Scenario05_PartPrincipalPayment_ReducesOutstanding` | **PASS** |
| Bank Custody Safety Check | Guard | Block closure if ornaments are in Bank Repledge | `Scenario11_ClosingCustomerLoanWithJewelsInBank_RaisesSafetyCheck` | **PASS** |
| Post Receipt Button | Button | Generates numbered receipt and balanced voucher | `Scenario05` & `Scenario06` | **PASS** |

---

### 2.4 Bank Repledge Entry & Payments (`BankRepledgeControl`, `BankPaymentControl`)
| Field Name | Type | Validation Rule | Test / Script Verification | Result |
|---|---|---|---|---|
| Bank Selection | Dropdown | Required, from Bank Master | `Scenario10_BankRepledge_DisbursesCashAndRecordsLiability` | **PASS** |
| Bank Pledge Number | Text | Required, unique | `Scenario10_BankRepledge_DisbursesCashAndRecordsLiability` | **PASS** |
| Disbursed Amount | Currency | > 0, <= Bank sanctioned limit | `Scenario10_BankRepledge_DisbursesCashAndRecordsLiability` | **PASS** |
| Bank Appraised Weight | Decimal | > 0, 3 decimal places | `Scenario10_BankRepledge_DisbursesCashAndRecordsLiability` | **PASS** |
| Maturity Date | DatePicker | >= Pledge Date | Date validation verified | **PASS** |
| Available Loans Grid | MultiSelect | Only unpledged active loans | Selection and weight tally verified | **PASS** |
| Bank Payment: Principal/Int | Currency | >= 0, updates bank liability | `ProcessBankPaymentAsync` verified | **PASS** |
| Packet Release to Locker | Checkbox | Returns ornaments to Shop Locker upon payment | `Scenario11` verified | **PASS** |

---

### 2.5 Overdue Notices & Auction (`NoticeAuctionControl`)
| Field Name | Type | Validation Rule | Test / Script Verification | Result |
|---|---|---|---|---|
| Overdue Search & Filter | Grid | Filter by Days Overdue (>30, >60, >90, >365) | `Scenario12_OverdueClassification_ClassifiesAgeCorrectly` | **PASS** |
| Notice Batch Generation | Action | Generates Legal Notice 1, 2, or Auction Notice | `IssueNoticeBatchAsync` verified | **PASS** |
| Auction Approval | Guard | Owner authorization required | Role permission checks verified | **PASS** |
| Pre-Auction Waiting Period | Guard | Minimum 15 days notice prior to auction date | `ApproveAndScheduleAuctionAsync` verified | **PASS** |
| Auction Settlement | Action | Distributes proceeds to Loan, Interest, Expenses, and Surplus | `Scenario13_AuctionSettlement_ComputesSurplusPayableToCustomer` | **PASS** |

---

### 2.6 Day End Close & Drawer Reconciliation (`DayEndCloseControl`)
| Field Name | Type | Validation Rule | Test / Script Verification | Result |
|---|---|---|---|---|
| System Cash on Hand | Currency | Calculated from Day Book balance | `Scenario14_DayEndClose_VerifiesCashDrawerIntegrity` | **PASS** |
| Physical Counted Cash | Currency | Required, >= 0 | Non-negative check verified | **PASS** |
| Cash Shortage / Overage | Currency | Auto-calculated difference | Auto-updating discrepancy alert verified | **PASS** |
| Auto-Backup on Close | Action | Encrypted backup created prior to date lock | `BackupService` integration verified | **PASS** |
| Close Register Button | Button | Posts cash diff voucher and locks date | `Scenario14` verified | **PASS** |

---

### 2.7 Accounts, Day Book & Financial Integrity (`DayBookEntryControl`, `AccountsIntegrityControl`)
| Field Name | Type | Validation Rule | Test / Script Verification | Result |
|---|---|---|---|---|
| Date Range Filter | DatePicker | From Date <= To Date | `GetDayBookAsync` verified | **PASS** |
| Opening & Closing Cash | Currency | Verified against Cash Ledger Head 1001 | `GetDayBookAsync` verified | **PASS** |
| Voucher Postings | Grid | Balanced debits and credits | `Scenario15_FullFinancialIntegrityAudit` | **PASS** |
| Integrity Audit Run | Action | Verifies all vouchers balanced, sub-ledger = GL | `Scenario15_FullFinancialIntegrityAudit` | **PASS** |
| Discrepancies Count | Number | **0 (Zero)** | `audit.UnbalancedVouchersCount == 0` | **PASS** |
| Balance Sheet Difference | Currency | **₹ 0.00** | `audit.BalanceSheetDifference == 0m` | **PASS** |

---

## 3. UI Redesign & Defect Resolution Summary

1. **Function Key Shortcut Removal**:
   - All `(F1)`, `(F2)`, `(F3)`, `(F4)`, `(F5)`, `(F6)`, `(F7)`, `(F8)`, `(F9)`, `(F10)`, `(Alt+F4)`, `(Enter)` labels removed from every button, menu item, and dialog.
   - Replaced with clear, accessible labels: `New Customer`, `Save Customer`, `Issue Loan`, `Post Receipt`, `Post Voucher`, `Print`, `Cancel`, `Search`, `Exit`.

2. **Unified Styling & Theme System**:
   - `Themes/Colors.xaml`: Deep Blue primary (`#1E3A8A`), slate hover (`#1D4ED8`), white surfaces (`#FFFFFF`), light background (`#F8FAFC`), crisp borders (`#CBD5E1`).
   - `Themes/Fonts.xaml`: Segoe UI with fallback to `Noto Sans Tamil`, unified font hierarchy (20px headers, 13px bold table headers, 14px body).
   - `Themes/Controls.xaml`: Standard 36px control height, consistent focus highlights, right-aligned monetary and weight figures, centered dates, alternating DataGrid row colors (`#F8FAFC`).

3. **Exception Safety & Stability**:
   - Removed `EnableRetryOnFailure` from `App.xaml.cs` to prevent `NpgsqlRetryingExecutionStrategy does not support user-initiated transactions`.
   - Explicitly wrapped all transactions across `LoanService`, `ReceiptService`, `BankRepledgeService`, `AccountingReportService`, `OverdueNoticeService`, and `SafeUpdateService` with `CreateExecutionStrategy()`.
   - Handled DI constructor resolution in `AuthService` and `App.xaml.cs` so startup never fails with ambiguous constructor exceptions.
   - All navigation routes protected via `SafeNavigate` with friendly warnings and Serilog error logging.

---

## 4. Known Issues & Residual Risk
- **Known Issues**: **0 (None)**.
- **Residual Risk**: In a multi-PC environment, ensure PostgreSQL port 5432 is open on the Server PC firewall (handled automatically by `installer.iss` in Server mode).

