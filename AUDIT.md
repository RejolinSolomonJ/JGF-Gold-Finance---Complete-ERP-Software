# JGF Finance Software — Comprehensive Application Audit & Defect Registry
**Document Version**: 1.0.0  
**Generated Date**: 09-Nov-2026  
**Target Environment**: WPF Desktop (.NET 8.0, C# 12, PostgreSQL 16)  
**Status**: AUDIT COMPLETE (Ready for Review Before Fixes)

---

## Executive Summary

An exhaustive, screen-by-screen, field-by-field audit of **JGF (Jeba Gold Finance)** was conducted across all 27 user controls, 6 dialog windows, application shell, and database services. The audit traced the end-to-end data pipeline for each field:  
`UI Control` → `XAML Binding / Event Handler` → `ViewModel Property / Code-Behind` → `Application Service` → `PostgreSQL Database Column`.

### Key Defect Categories Identified:
1. **Broken UI Data Flow & Missing Reactive Updates**:
   - `LoanItemRowViewModel` in `NewLoanEntryControl` does not implement `INotifyPropertyChanged`. Edits to Gross Weight or Deduction Weight do not notify the UI or automatically recalculate Net Weight and Appraised Value on cell edit.
   - TextChanged auto-overwrite bug: Typing in `TxtLoanAmount` resets the value to `maxEligible` whenever temporary values (such as backspace to 0) are entered.
   - Customer picker in `NewLoanEntryControl` does not display a lookup/picker modal; it hardcodes the first customer returned from `SearchCustomersAsync("", 1)`.
2. **Missing Validation & Negative Amount Vulnerabilities**:
   - Amount and number textboxes rely on `decimal.TryParse` without non-negative checks (`>= 0`). Entering negative values (e.g., `-500` in Part Principal or `-100` in Interest) passes parsing and corrupts the ledger vouchers.
   - Mobile numbers accept arbitrary text lengths rather than enforcing 10 digits (`^\d{10}$`).
   - Aadhaar numbers accept letters without verifying 12 numeric digits (`^\d{12}$`).
   - PAN numbers do not enforce Indian tax format (`^[A-Z]{5}[0-9]{4}[A-Z]$`).
   - Pincode does not enforce 6 digits (`^\d{6}$`).
   - Save buttons remain enabled even when required fields are missing or invalid; there are no inline red error indicators below fields.
3. **Legacy Function-Key Shortcuts & Inconsistent Styling**:
   - Menu headers and button labels contain hardcoded function keys (`(F1)`, `(F2)`, `(F3)`, `(F4)`, `(F5)`, `(F6)`, `(F7)`, `(F8)`, `(F9)`, `(F10)`).
   - Inconsistent inline hex colors (`#B8860B`, `#2E7D32`, `#C62828`, `#2B2B2B`) and font sizes throughout XAML instead of centralized application style resources.
   - Missing standard control heights (36px), missing TabIndex logical sequencing, and lack of Enter-to-next-field keyboard navigation.
4. **Hardcoded User Context**:
   - `NewLoanEntryControl` and `LoanReceiptControl` disburse loans and receipts using `Guid.Empty` and hardcoded `UserRole.Owner` instead of passing the active authenticated user session.
5. **Data Formatting Flaws**:
   - Grid amounts and weights lack consistent right-alignment.
   - Weights in some grids are formatted to 2 decimals instead of standard 3 decimals (`0.000 g`).
   - Dates in several controls are not centered or standardized to `dd-MMM-yyyy`.

---

## 1. Master Screen & Field Audit Table

| Screen / View | Field / Control Name | Control Type | Req / Opt | Target Type | Validation Rule | Data Source / Binding | DB Column | Current Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **MainWindow** | Top Menu Navigation | `Menu` | Req | UI Nav | Valid role permission | Role-based logic | `users.role` | **Broken**: Contains legacy `(F1)`–`(F10)` shortcut labels |
| MainWindow | User Badge | `TextBlock` | Req | String | Authenticated user | `_currentUser.FullName` | `users.full_name` | **Works** |
| MainWindow | Role Badge | `TextBlock` | Req | Enum | Owner, Manager, Cashier | `_currentUser.Role` | `users.role` | **Works** |
| MainWindow | Date Badge | `TextBlock` | Req | String | System current date | `DateTime.Now.ToString("dd-MMM-yyyy")` | N/A | **Works** |
| MainWindow | DB Status | `TextBlock` | Req | String | Connection ping | `NpgsqlConnection.CanConnect` | N/A | **Works** |
| MainWindow | Backup Alert | `TextBlock` | Req | String | Warning if > 24h since backup | `IBackupService.HasRecentSuccessfulBackupAsync` | `backup_logs` | **Works** |
| **LoginDialog** | Username (`TxtUsername`) | `TextBox` | Req | String | Non-empty | Code-behind `TxtUsername.Text` | `users.username` | **Works** |
| LoginDialog | Password (`TxtPassword`) | `PasswordBox` | Req | String | Non-empty | Code-behind `TxtPassword.Password` | `users.password_hash` | **Works** |
| LoginDialog | Login Button | `Button` | Req | Action | Valid credentials | Click event `OnLoginClick` | N/A | **Broken**: Label says `Login (Enter)` |
| LoginDialog | Cancel Button | `Button` | Req | Action | None | Click event `OnCancelClick` | N/A | **Works** |
| **FirstRunWizardDialog** | DbHost (`TxtDbHost`) | `TextBox` | Req | String | Hostname / IP format | Code-behind | N/A | **Works** |
| FirstRunWizardDialog | DbPort (`TxtDbPort`) | `TextBox` | Req | Int | 1 to 65535 | Code-behind | N/A | **Works** |
| FirstRunWizardDialog | DbName (`TxtDbName`) | `TextBox` | Req | String | Valid SQL identifier | Code-behind | N/A | **Works** |
| FirstRunWizardDialog | DbUser (`TxtDbUser`) | `TextBox` | Req | String | Non-empty | Code-behind | N/A | **Works** |
| FirstRunWizardDialog | DbPassword (`TxtDbPass`) | `PasswordBox` | Req | String | Non-empty | Code-behind | N/A | **Works** |
| FirstRunWizardDialog | Owner Username | `TextBox` | Req | String | Min 3 chars | Code-behind | `users.username` | **Works** |
| FirstRunWizardDialog | Owner Full Name | `TextBox` | Req | String | Non-empty | Code-behind | `users.full_name` | **Works** |
| FirstRunWizardDialog | Owner Password | `PasswordBox` | Req | String | Min 8 chars, 1 number, 1 upper | Code-behind | `users.password_hash` | **Broken**: No inline validation feedback |
| FirstRunWizardDialog | Company Legal Name | `TextBox` | Req | String | Non-empty | Code-behind | `settings.value` | **Works** |
| FirstRunWizardDialog | Tamil Name | `TextBox` | Opt | String | Tamil script | Code-behind | `settings.value` | **Works** |
| FirstRunWizardDialog | Pawn License No | `TextBox` | Req | String | Non-empty | Code-behind | `settings.value` | **Works** |
| FirstRunWizardDialog | GSTIN | `TextBox` | Opt | String | 15-digit GST format | Code-behind | `settings.value` | **Broken**: Missing regex format validation |
| **CustomerMasterControl** | Search Box (`TxtSearch`) | `TextBox` | Opt | String | Min 1 char | Debounced TextChanged event | `customers` table | **Works** |
| CustomerMasterControl | Customer List (`GridCustomers`)| `DataGrid` | Req | Collection | None | `ItemsSource = List<Customer>` | `customers` | **Broken**: No pagination, columns unstyled |
| CustomerMasterControl | Customer Code (`TxtCode`) | `TextBox` | Req | String | Auto-generated | Read-only | `customers.code` | **Broken**: Client generates `C` + timestamp instead of server series |
| CustomerMasterControl | Mobile Number (`TxtMobile`) | `TextBox` | Req | String | Exact 10 digits (`^\d{10}$`) | Code-behind `TxtMobile.Text` | `customers.mobile_number` | **Broken**: Accepts non-numeric & any length |
| CustomerMasterControl | Full Name (English) (`TxtFullName`)| `TextBox` | Req | String | Min 2 characters | Code-behind `TxtFullName.Text` | `customers.full_name` | **Works** |
| CustomerMasterControl | பெயர் (Tamil) (`TxtNameTamil`)| `TextBox` | Opt | String | Unicode Tamil | Code-behind `TxtNameTamil.Text` | `customers.name_tamil` | **Works** |
| CustomerMasterControl | Guardian Name (`TxtGuardian`) | `TextBox` | Opt | String | Optional | Code-behind `TxtGuardian.Text` | `customers.guardian_name`| **Works** |
| CustomerMasterControl | Aadhaar (`TxtAadhaar`) | `TextBox` | Opt | String | Exactly 12 digits | Code-behind `TxtAadhaar.Text` | `customers.aadhaar_number`| **Broken**: Accepts non-digits, no regex validation |
| CustomerMasterControl | PAN Number (`TxtPan`) | `TextBox` | Opt | String | 10 chars Indian PAN regex | Code-behind `TxtPan.Text` | `customers.pan_number` | **Broken**: No PAN regex check |
| CustomerMasterControl | Photo Path (`ImgCustomerPhoto`)| `Image` | Opt | Image URI | Valid image file | `c.PhotoPath` | `customers.photo_path` | **Broken**: Stored in app root rather than ProgramData |
| CustomerMasterControl | Address (`TxtAddress`) | `TextBox` | Opt | String | Multiline | Code-behind `TxtAddress.Text` | `customers.address` | **Works** |
| CustomerMasterControl | City (`TxtCity`) | `TextBox` | Opt | String | Non-empty | Code-behind `TxtCity.Text` | `customers.city` | **Works** |
| CustomerMasterControl | District (`TxtDistrict`) | `TextBox` | Opt | String | Non-empty | Code-behind `TxtDistrict.Text` | `customers.district` | **Works** |
| CustomerMasterControl | Pincode (`TxtPincode`) | `TextBox` | Opt | String | Exactly 6 digits | Code-behind `TxtPincode.Text` | `customers.pincode` | **Broken**: No 6-digit numeric validation |
| CustomerMasterControl | Save Customer Button | `Button` | Req | Action | Form must be valid | Click event `OnSaveCustomerClick` | `ICustomerService` | **Broken**: Labeled `Save Customer (F10)` |
| CustomerMasterControl | Soft Delete Button | `Button` | Opt | Action | Confirm prompt | Click event `OnDeleteCustomerClick` | `customers.is_deleted` | **Works** |
| **NewLoanEntryControl** | Customer Pick (`TxtCustomerSearch`)| `TextBox` | Req | String | Valid customer selected | `_selectedCustomer.FullName` | `loans.customer_id` | **Broken**: "Pick" button hardcoded to first customer |
| NewLoanEntryControl | Scheme Dropdown (`CmbScheme`) | `ComboBox` | Req | Scheme | Active schemes | `ISchemeService.GetAllSchemesAsync`| `loans.scheme_id` | **Works** |
| NewLoanEntryControl | Pledge Date (`DpPledgeDate`) | `DatePicker` | Req | DateOnly | Not in future | SelectedDate | `loans.pledge_date` | **Broken**: Can select future dates |
| NewLoanEntryControl | Packet No (`TxtPacket`) | `TextBox` | Req | String | Non-empty | Code-behind `TxtPacket.Text` | `loans.packet_number` | **Works** |
| NewLoanEntryControl | Nominee Name (`TxtNominee`) | `TextBox` | Opt | String | Optional | Code-behind `TxtNominee.Text` | `loans.nominee_name` | **Works** |
| NewLoanEntryControl | Relationship (`TxtNomineeRel`) | `TextBox` | Opt | String | Optional | Code-behind `TxtNomineeRel.Text` | `loans.nominee_relationship`| **Works** |
| NewLoanEntryControl | Jewel Scale Photo | `TextBox` | Opt | Path | Valid image | FileDialog | `loans.jewel_photo_path` | **Works** |
| NewLoanEntryControl | Item Description | `DataGridColumn`| Req | String | Non-empty | `LoanItemRowViewModel.Description` | `loan_items.description` | **Works** |
| NewLoanEntryControl | Item Quantity | `DataGridColumn`| Req | Int | >= 1 | `LoanItemRowViewModel.Quantity` | `loan_items.quantity` | **Works** |
| NewLoanEntryControl | Item Gross Weight | `DataGridColumn`| Req | Decimal | > 0, 3 decimals | `LoanItemRowViewModel.GrossWeight` | `loan_items.gross_weight`| **Broken**: No INotifyPropertyChanged; row net wt stale |
| NewLoanEntryControl | Item Deduction Wt | `DataGridColumn`| Req | Decimal | >= 0, 3 decimals | `LoanItemRowViewModel.DeductionWeight`| `loan_items.deduction_weight`| **Broken**: No INotifyPropertyChanged; doesn't auto-update row |
| NewLoanEntryControl | Item Net Weight | `DataGridColumn`| ReadOnly | Decimal | Gross - Deduct | `LoanItemRowViewModel.NetWeight` | `loan_items.net_weight` | **Broken**: Formula correct, but UI does not refresh upon cell edit |
| NewLoanEntryControl | Item Purity % | `DataGridColumn`| Req | Decimal | 0.00 to 100.00 | `LoanItemRowViewModel.PurityPercent` | `loan_items.purity_percent`| **Works** |
| NewLoanEntryControl | Item Rate/g | `DataGridColumn`| Req | Decimal | > 0, 2 decimals | `LoanItemRowViewModel.RatePerGram` | `loan_items.rate_per_gram`| **Works** |
| NewLoanEntryControl | Item Appraised Value | `DataGridColumn`| ReadOnly | Decimal | NetWt * Rate * (Purity/100)| `LoanItemRowViewModel.AppraisedValue`| `loan_items.appraised_value`| **Broken**: Stale until whole grid loses focus |
| NewLoanEntryControl | Total Net Weight | `TextBlock` | ReadOnly | Decimal | Sum of net weights | `TxtTotalNet.Text` | N/A | **Broken**: Stale during inline row editing |
| NewLoanEntryControl | Total Market Value | `TextBlock` | ReadOnly | Decimal | Sum of appraised values | `TxtTotalValue.Text` | N/A | **Broken**: Stale during inline row editing |
| NewLoanEntryControl | Max LTV Eligible | `TextBlock` | ReadOnly | Decimal | MarketValue * Ltv% | `TxtMaxEligible.Text` | N/A | **Works** |
| NewLoanEntryControl | Monthly Interest Est | `TextBlock` | ReadOnly | Decimal | LoanAmt * (Rate%/mo) | `TxtEstMonthlyInterest.Text` | N/A | **Works** |
| NewLoanEntryControl | Loan Amount Disbursed | `TextBox` | Req | Decimal | > 0, <= Max LTV without override | `TxtLoanAmount.Text` | `loans.loan_amount` | **Broken**: Overwrites user input to max on typing; allows negative values |
| NewLoanEntryControl | Override Notice | `TextBlock` | Opt | Alert | Shown if LoanAmt > MaxEligible | Visibility binding | N/A | **Works** |
| NewLoanEntryControl | Override Reason | `TextBox` | Opt | String | Required if LoanAmt > MaxEligible | `TxtOverrideReason.Text` | `loans.override_reason` | **Broken**: Not enforced as mandatory when LTV exceeded |
| NewLoanEntryControl | Issue Loan Button | `Button` | Req | Action | Valid form & customer | `OnIssueLoanClick` | `ILoanService` | **Broken**: Labeled `Issue Loan & Print Ticket (F10)`; hardcodes `Guid.Empty` user |
| **LoanReceiptControl** | Search Query (`TxtSearchQuery`)| `TextBox` | Req | String | GL Number / Mobile | Code-behind | `loans` table | **Works** |
| LoanReceiptControl | Receipt Date (`DpReceiptDate`)| `DatePicker` | Req | DateOnly | >= PledgeDate & <= Today | SelectedDate | `receipts.receipt_date` | **Broken**: Allows dates before pledge date |
| LoanReceiptControl | Customer Name & Mobile | `TextBlock` | ReadOnly | String | Loaded loan customer | `_loadedLoan.Customer` | `customers` | **Works** |
| LoanReceiptControl | Principal Balance | `TextBlock` | ReadOnly | Decimal | Active principal | `_loadedLoan.PrincipalBalance` | `loans.principal_balance`| **Works** |
| LoanReceiptControl | Interest Table (`GridInterest`)| `DataGrid` | ReadOnly | Breakdown | Period by period calculation | `IInterestCalculator` | `receipts` calculation | **Broken**: Numbers not right aligned, dates not centered |
| LoanReceiptControl | Receipt Type (`CmbReceiptType`)| `ComboBox` | Req | Enum | InterestOnly, PartPrincipal, Closure| Code-behind | `receipts.receipt_type` | **Works** |
| LoanReceiptControl | Accrued Interest (`TxtInterestAmount`)| `TextBox` | Req | Decimal | >= 0, 2 decimals | `TxtInterestAmount.Text` | `receipts.interest_amount`| **Broken**: Allows negative numbers |
| LoanReceiptControl | Part Principal (`TxtPartPrincipal`)| `TextBox` | Req | Decimal | >= 0 & <= PrincipalBalance | `TxtPartPrincipal.Text` | `receipts.principal_amount`| **Broken**: Allows negative numbers; exceeds balance |
| LoanReceiptControl | Postal Charges (`TxtPostalCharges`)| `TextBox` | Opt | Decimal | >= 0, 2 decimals | `TxtPostalCharges.Text` | `receipts.postal_charges` | **Broken**: Allows negative numbers |
| LoanReceiptControl | Round-Off Adj (`TxtAdjustment`)| `TextBox` | Opt | Decimal | >= 0, 2 decimals | `TxtAdjustment.Text` | `receipts.adjustment_amount`| **Broken**: Allows negative numbers |
| LoanReceiptControl | Payment Mode (`CmbPayMode`) | `ComboBox` | Req | Enum | Cash, UPI, BankTransfer, Cheque | Code-behind | `receipts.payment_mode` | **Works** |
| LoanReceiptControl | Total Receivable | `TextBlock` | ReadOnly | Decimal | Sum(components) - adjustment | `TxtTotalReceivable.Text` | `receipts.total_amount` | **Works** |
| LoanReceiptControl | Save Receipt Button | `Button` | Req | Action | Total > 0 & valid form | `OnSaveReceiptClick` | `IReceiptService` | **Broken**: Labeled `Process Receipt & Print (F10)`; hardcodes `Guid.Empty` user |
| **BankRepledgeControl** | Repledge Dashboard Grid | `DataGrid` | ReadOnly | Collection | Bank Pledges overview | `GridPledges.ItemsSource` | `bank_pledges` | **Works** |
| BankRepledgeControl | Make Payment Button | `Button` | Req | Action | Navigates to payment tab | DataGrid Row Button | N/A | **Works** |
| **BankRepledgeNewEntryControl**| Bank Dropdown (`CmbBank`)| `ComboBox` | Req | Bank | Active bank selection | `IBankService.GetAllBanksAsync` | `bank_pledges.bank_id` | **Works** |
| BankRepledgeNewEntryControl| Bank Pledge No (`TxtPledgeNumber`)| `TextBox` | Req | String | Non-empty unique | Code-behind `TxtPledgeNumber.Text`| `bank_pledges.pledge_number`| **Works** |
| BankRepledgeNewEntryControl| Pledging Date (`DpPledgeDate`)| `DatePicker` | Req | DateOnly | Not in future | SelectedDate | `bank_pledges.pledge_date` | **Broken**: No future date validation |
| BankRepledgeNewEntryControl| Maturity Date (`DpMaturityDate`)| `DatePicker`| Req | DateOnly | > PledgeDate | SelectedDate | `bank_pledges.maturity_date`| **Broken**: Can be earlier than pledge date |
| BankRepledgeNewEntryControl| Bank Interest Rate (`TxtInterestRate`)| `TextBox` | Req | Decimal | > 0, 2 decimals | Code-behind `TxtInterestRate.Text`| `bank_pledges.interest_rate`| **Broken**: Allows negative rate |
| BankRepledgeNewEntryControl| Bank Appraised Weight | `TextBox` | Opt | Decimal | >= 0, 3 decimals | `TxtBankWeight.Text` | `bank_pledges.bank_appraised_weight`| **Broken**: Allows negative weight |
| BankRepledgeNewEntryControl| Bank Loan A/C No | `TextBox` | Req | String | Non-empty | `TxtBankAccountNo.Text` | `bank_pledges.bank_account_or_loan_number`| **Works** |
| BankRepledgeNewEntryControl| Available Customer Loans | `DataGrid` | Req | Items | In Shop Locker only | `GridAvailableLoans.ItemsSource`| `loans` | **Works** |
| BankRepledgeNewEntryControl| Bank Disbursed Amount | `TextBox` | Req | Decimal | > 0, 2 decimals | `TxtBankDisbursedAmount.Text` | `bank_pledges.disbursed_amount`| **Broken**: Allows negative numbers |
| BankRepledgeNewEntryControl| Save Bank Pledge Button | `Button` | Req | Action | Min 1 loan selected, valid amount | `OnSaveBankPledgeClick` | `IBankRepledgeService` | **Broken**: Labeled `Save Bank Pledge & Post Voucher (F10)` |
| **BankPaymentControl** | Search Bank Pledge (`TxtSearchQuery`)| `TextBox` | Req | String | Non-empty | Code-behind | `bank_pledges` | **Works** |
| BankPaymentControl | Payment Date (`DpPaymentDate`)| `DatePicker` | Req | DateOnly | Not in future | SelectedDate | `bank_payments.payment_date`| **Works** |
| BankPaymentControl | Linked Loans Grid | `DataGrid` | Req | Items | Linked loans | `GridLinkedLoans.ItemsSource` | `bank_pledge_loans` | **Works** |
| BankPaymentControl | Payment Action (`CmbPaymentType`)| `ComboBox` | Req | Enum | Interest, PartPrincipal, Closure | Code-behind | `bank_payments` | **Works** |
| BankPaymentControl | Interest Paid (`TxtInterestPaid`)| `TextBox` | Req | Decimal | >= 0, 2 decimals | `TxtInterestPaid.Text` | `bank_payments.interest_paid`| **Broken**: Allows negative numbers |
| BankPaymentControl | Principal Paid (`TxtPrincipalPaid`)| `TextBox` | Req | Decimal | >= 0, 2 decimals | `TxtPrincipalPaid.Text` | `bank_payments.principal_paid`| **Broken**: Allows negative numbers |
| BankPaymentControl | Bank Charges (`TxtPostalCharges`)| `TextBox` | Opt | Decimal | >= 0, 2 decimals | `TxtPostalCharges.Text` | `bank_payments.other_charges`| **Broken**: Allows negative numbers |
| BankPaymentControl | Total Outflow | `TextBlock` | ReadOnly | Decimal | Interest + Principal + Charges | `TxtTotalOutflow.Text` | `bank_payments.total_paid`| **Works** |
| BankPaymentControl | Post Bank Payment Button | `Button` | Req | Action | Total > 0 | `OnSaveBankPaymentClick` | `IBankRepledgeService` | **Works** |
| **NoticeAuctionControl** | Filter Category Dropdown | `ComboBox` | Req | Enum | 6-Mo, 12-Mo, Critical LTV, Auction| SelectionChanged event | `IOverdueNoticeService` | **Works** |
| NoticeAuctionControl | Search Overdue Loans | `TextBox` | Opt | String | Debounced filter | TextChanged event | N/A | **Works** |
| NoticeAuctionControl | Overdue Loans DataGrid | `DataGrid` | Req | Items | Overdue loans | `GridOverdueLoans.ItemsSource` | `loans` | **Broken**: Numbers not right-aligned |
| NoticeAuctionControl | Notice Type Dropdown | `ComboBox` | Req | Enum | Reminder, Final, AuctionNotice | SelectionChanged event | `notices.notice_type` | **Works** |
| NoticeAuctionControl | Grace Period Days | `TextBox` | Req | Int | >= 1 | Code-behind | `notices.grace_period_days`| **Broken**: Can accept negative or zero days |
| NoticeAuctionControl | Postal Charge (`TxtPostalCharge`)| `TextBox` | Opt | Decimal | >= 0, 2 decimals | Code-behind | `notices.postal_charge` | **Broken**: Allows negative numbers |
| NoticeAuctionControl | Generate Notice Button | `Button` | Req | Action | Valid selected loans | `OnGenerateNoticesClick` | `IOverdueNoticeService` | **Works** |
| NoticeAuctionControl | Auction Date (`DpAuctionDate`)| `DatePicker` | Req | DateOnly | >= 15 days in future | SelectedDate | `auctions.auction_date` | **Broken**: No 15-day pre-notice date check |
| NoticeAuctionControl | Auctioneer Name | `TextBox` | Req | String | Non-empty | Code-behind | `auctions.auctioneer_name`| **Works** |
| NoticeAuctionControl | Venue | `TextBox` | Req | String | Non-empty | Code-behind | `auctions.venue` | **Works** |
| NoticeAuctionControl | Authorize Auction Button | `Button` | Req | Action | Owner role only | `OnAuthorizeAuctionClick` | `IOverdueNoticeService` | **Works** |
| **DayBookEntryControl** | From & To Dates | `DatePicker` | Req | DateOnly | From <= To | SelectedDate | `vouchers.voucher_date` | **Works** |
| DayBookEntryControl | Cash Position Badges | `TextBlock` | ReadOnly | Decimal | Inflow, Outflow, Closing | Calculated | N/A | **Works** |
| DayBookEntryControl | Day Book Grid | `DataGrid` | ReadOnly | Items | Posted vouchers | `GridDayBook.ItemsSource` | `vouchers`, `voucher_lines`| **Broken**: Numbers not right-aligned |
| DayBookEntryControl | Voucher Type (`CmbVoucherType`)| `ComboBox` | Req | Enum | Receipt, Payment | SelectionChanged | `vouchers.voucher_type` | **Works** |
| DayBookEntryControl | Ledger Head (`CmbLedgerHead`) | `ComboBox` | Req | Ledger | Active ledger heads | `IAccountingService.GetLedgerHeads`| `voucher_lines.ledger_head_id`| **Works** |
| DayBookEntryControl | Amount (`TxtAmount`) | `TextBox` | Req | Decimal | > 0, 2 decimals | `TxtAmount.Text` | `vouchers.total_amount` | **Broken**: Allows negative numbers |
| DayBookEntryControl | Narration (`TxtNarration`) | `TextBox` | Opt | String | Optional description | `TxtNarration.Text` | `vouchers.narration` | **Works** |
| DayBookEntryControl | Post Voucher Button | `Button` | Req | Action | Valid head & amount > 0 | `OnSaveVoucherClick` | `IAccountingService` | **Works** |
| **DayEndCloseControl** | Day Status Banner | `Border` | ReadOnly | Status | OPEN or CLOSED | `IDayCloseService.GetDayStatus` | `day_closes.is_closed` | **Works** |
| DayEndCloseControl | Physical Cash (`TxtPhysicalCash`)| `TextBox` | Req | Decimal | >= 0, 2 decimals | `TxtPhysicalCash.Text` | `day_closes.physical_cash_counted`| **Broken**: Allows negative numbers |
| DayEndCloseControl | Drawer Verified CheckBox | `CheckBox` | Req | Bool | Must be checked | `ChkVerified.IsChecked` | N/A | **Works** |
| DayEndCloseControl | Seal Day Button | `Button` | Req | Action | Physical cash entered & checked | `OnSealDayClick` | `IDayCloseService` | **Works** |
| **AccountsIntegrityControl**| Audit Date (`DpAuditDate`)| `DatePicker` | Req | DateOnly | Not in future | SelectedDate | N/A | **Works** |
| AccountsIntegrityControl| Run Audit Button | `Button` | Req | Action | Runs all 6 integrity checks | `OnRunAuditClick` | `IAccountingReportService` | **Works** |
| **FinancialReportsControl**| Report Selector (`CmbReportType`)| `ComboBox` | Req | Enum | Trial Balance, P&L, Balance Sheet| SelectionChanged | `IAccountingReportService` | **Works** |
| FinancialReportsControl| Date Range (`DpDate`) | `DatePicker` | Req | DateOnly | As-of date | SelectedDate | N/A | **Works** |
| FinancialReportsControl| Trial Balance DataGrid | `DataGrid` | ReadOnly | Items | Debit & Credit balances | `GridTrialBalance.ItemsSource` | `voucher_lines` | **Broken**: Numbers not right-aligned |
| FinancialReportsControl| P&L Expense & Income Grids | `DataGrid` | ReadOnly | Items | Grouped expense & income | ItemsSource | `voucher_lines` | **Broken**: Numbers not right-aligned |
| FinancialReportsControl| Balance Sheet Grids | `DataGrid` | ReadOnly | Items | Assets, Liabilities & Capital | ItemsSource | `ledger_heads` | **Broken**: Numbers not right-aligned |
| **JlReportsControl** | Report Selector (`CmbReportSelect`)| `ComboBox` | Req | Enum | 10 Gold Loan Reports | SelectionChanged | `IJlReportService` | **Works** |
| JlReportsControl | From & To DatePickers | `DatePicker` | Opt | DateOnly | Range | SelectedDate | N/A | **Works** |
| JlReportsControl | Scheme & Status Filters | `ComboBox` | Opt | Enum | Optional filters | SelectedValue | N/A | **Works** |
| JlReportsControl | Report DataGrids (10 Grids) | `DataGrid` | ReadOnly | Items | Report rows | Dynamic grids | `loans`, `receipts`, `loan_items`| **Broken**: Header and cell alignment unformatted |
| JlReportsControl | Export Excel Button | `Button` | Opt | Action | Exports current report to xlsx | `OnExportExcelClick` | `IExcelExportService` | **Works** |
| JlReportsControl | Print / PDF Button | `Button` | Opt | Action | Renders printable view | `OnPrintPdfClick` | FlowDocument / Print | **Works** |
| **SchemeMasterControl** | Scheme Code (`TxtSchemeCode`)| `TextBox` | Req | String | Unique non-empty | `TxtSchemeCode.Text` | `schemes.code` | **Works** |
| SchemeMasterControl | Scheme Name (`TxtSchemeName`)| `TextBox` | Req | String | Non-empty | `TxtSchemeName.Text` | `schemes.name` | **Works** |
| SchemeMasterControl | Monthly Rate (`TxtInterestRate`)| `TextBox` | Req | Decimal | > 0, 2 decimals | `TxtInterestRate.Text` | `schemes.interest_rate` | **Broken**: Allows negative rate |
| SchemeMasterControl | Penal Rate (`TxtPenalRate`)| `TextBox` | Opt | Decimal | >= 0, 2 decimals | `TxtPenalRate.Text` | `schemes.penal_rate` | **Broken**: Allows negative rate |
| SchemeMasterControl | Max LTV % (`TxtMaxLtv`) | `TextBox` | Req | Decimal | 1.00 to 100.00 | `TxtMaxLtv.Text` | `schemes.max_ltv_percentage`| **Broken**: Allows negative or > 100% |
| SchemeMasterControl | Tenure Months (`TxtTenureMonths`)| `TextBox` | Req | Int | 1 to 60 | `TxtTenureMonths.Text` | `schemes.tenure_months` | **Broken**: Allows negative months |
| SchemeMasterControl | Grace Days (`TxtGraceDays`) | `TextBox` | Opt | Int | >= 0 | `TxtGraceDays.Text` | `schemes.grace_period_days`| **Broken**: Allows negative days |
| SchemeMasterControl | Save Scheme Button | `Button` | Req | Action | Valid form | `OnSaveSchemeClick` | `ISchemeService` | **Works** |
| **GoldRateMasterControl** | Rate Date (`DpDate`) | `DatePicker` | Req | DateOnly | Today / Valid date | SelectedDate | `gold_rates.date` | **Works** |
| GoldRateMasterControl | 22K Rate (`Txt22KRate`) | `TextBox` | Req | Decimal | > 0, 2 decimals | `Txt22KRate.Text` | `gold_rates.rate_22k` | **Broken**: Allows negative rate |
| GoldRateMasterControl | 18K Rate (`Txt18KRate`) | `TextBox` | Opt | Decimal | > 0, 2 decimals | `Txt18KRate.Text` | `gold_rates.rate_18k` | **Broken**: Allows negative rate |
| GoldRateMasterControl | Market Rate (`TxtMarketRate`)| `TextBox` | Opt | Decimal | > 0, 2 decimals | `TxtMarketRate.Text` | `gold_rates.rate_per_gram`| **Broken**: Allows negative rate |
| GoldRateMasterControl | Save Rate Button | `Button` | Req | Action | Positive rates | `OnSaveRateClick` | `IGoldRateService` | **Broken**: Labeled `Save Today's Rate (F10)` |
| **JewelTypeMasterControl**| Code & Name (`TxtCode`, `TxtName`)| `TextBox` | Req | String | Non-empty unique | Code-behind | `jewel_types` | **Works** |
| JewelTypeMasterControl | Save Jewel Type Button | `Button` | Req | Action | Valid name & code | `OnSaveClick` | `IJewelTypeService` | **Works** |
| **BankMasterControl** | Bank Code & Name | `TextBox` | Req | String | Non-empty unique | Code-behind | `banks` | **Works** |
| BankMasterControl | Account No & IFSC | `TextBox` | Opt | String | Valid IFSC format | Code-behind | `banks.account_number`, `ifsc`| **Broken**: No IFSC format check |
| BankMasterControl | Interest Rate Charged | `TextBox` | Req | Decimal | > 0, 2 decimals | `TxtInterestRate.Text` | `banks.interest_rate` | **Broken**: Allows negative rate |
| BankMasterControl | Sanctioned Limit | `TextBox` | Opt | Decimal | >= 0, 2 decimals | `TxtSanctionedLimit.Text` | `banks.sanctioned_limit` | **Broken**: Allows negative limit |
| BankMasterControl | Save Bank Profile Button | `Button` | Req | Action | Valid bank form | `OnSaveBankClick` | `IBankService` | **Works** |
| **LedgerHeadMasterControl**| Code & Name | `TextBox` | Req | String | Unique code & name | Code-behind | `ledger_heads` | **Works** |
| LedgerHeadMasterControl| Group & Normal Balance | `ComboBox` | Req | Enum | Asset, Liability, Capital, etc.| Code-behind | `ledger_heads.account_group` | **Works** |
| LedgerHeadMasterControl| Save Ledger Head Button | `Button` | Req | Action | Valid account | `OnSaveHeadClick` | `IAccountingService` | **Works** |
| **NumberSeriesMasterControl**| Series Grid Editing | `DataGrid` | Req | Collection | Gapless sequential numbers | `GridSeries.ItemsSource` | `number_series` | **Works** |
| **AuditLogControl** | Table & Date Range Filters | `ComboBox` / `DatePicker` | Opt | Filter | Optional filter | Code-behind | `audit_logs` | **Works** |
| **BackupSettingsControl** | Master Key (`TxtKey`) | `PasswordBox` | Req | String | Min 8 chars | Code-behind | AES-256 Key derivation | **Works** |
| BackupSettingsControl | LAN Shared Destination | `TextBox` | Opt | Path | Valid network UNC path | `TxtLanFolder.Text` | `settings` | **Works** |
| BackupSettingsControl | Cloud Sync Folder | `TextBox` | Opt | Path | Valid local directory | `TxtCloudFolder.Text` | `settings` | **Works** |
| BackupSettingsControl | Run Backup Button | `Button` | Req | Action | Triggers pg_dump + AES-256 | `OnRunBackupClick` | `IBackupService` | **Works** |
| BackupSettingsControl | Test Restore Button | `Button` | Opt | Action | Restores to test DB | `OnTestRestoreClick` | `IBackupService` | **Works** |
| **DbConnectionSettingsControl**| Host, Port, DB, User, Pass| `TextBox` / `PasswordBox`| Req | Config | Connection string parameters | Code-behind | `appsettings.json` | **Works** |
| DbConnectionSettingsControl| Test Connection Button | `Button` | Req | Action | Live ping | `OnTestConnectionClick` | `NpgsqlConnection` | **Works** |
| DbConnectionSettingsControl| Save Settings Button | `Button` | Req | Action | Updates config & notifies App | `OnSaveConnectionClick` | Configuration File | **Works** |
| **DashboardControl** | Refresh Button | `Button` | Req | Action | Reloads all 8 KPI cards | `OnRefreshClick` | `IDashboardService` | **Works** |
| DashboardControl | Quick Action Buttons | `Button` | Req | Navigation | Switches workspace view | Click handlers | N/A | **Works** |
| **AboutDialog** | Version & History Grid | `DataGrid` | ReadOnly | List | Migration & release records | `release_history` | `schema_versions` | **Works** |
| **ScreenLockDialog** | Password Unlock | `PasswordBox` | Req | String | Current user password | Code-behind | `IPasswordHasher` | **Works** |
| **SafeUpdateWizardDialog**| Safe Update Execution | `Button` | Req | Action | Verifies backup + executes migration | `OnRunUpdateClick` | `ISafeUpdateService` | **Works** |

---

## 2. Traced Full Data Pipeline & Detailed Defect Root Causes

### 2.1 Loan Item Calculation Desynchronization
- **Path**: `GridItems` DataGrid TextCell → `LoanItemRowViewModel.GrossWeight` / `DeductionWeight` → `NetWeight` property getter → `NewLoanEntryControl.RecalculateTotals()` → `CreateLoanDto` → `LoanService.IssueNewLoanAsync` → `loans.net_weight` / `loan_items.net_weight`.
- **Root Cause**: `LoanItemRowViewModel` is a POCO class without `INotifyPropertyChanged`. WPF DataGrid only updates cell values on lost focus, but dependent computed properties (`NetWeight`, `AppraisedValue`) never emit `PropertyChanged` events. Furthermore, `CellEditEnding` fires before the underlying binding source updates, resulting in `RecalculateTotals()` reading stale gross/deduction weights.
- **Fix Required**:
  1. Inherit `LoanItemRowViewModel` from `ObservableObject` (or implement `INotifyPropertyChanged`).
  2. Fire `OnPropertyChanged(nameof(NetWeight))` and `OnPropertyChanged(nameof(AppraisedValue))` within the setters of `GrossWeight`, `DeductionWeight`, `RatePerGram`, and `PurityPercent`.
  3. Ensure `NetWeight = Math.Max(0m, GrossWeight - DeductionWeight)`.

### 2.2 Typing Overwrite Bug on Loan Amount
- **Path**: `TxtLoanAmount` TextBox → `OnLoanAmountChanged` event → `RecalculateTotals()`.
- **Root Cause**: `RecalculateTotals()` checks `if (loanAmt <= 0) { TxtLoanAmount.Text = maxEligible.ToString("F2"); }`. When a user presses backspace to clear the field or enters `0` to type a new amount, `OnLoanAmountChanged` fires, parses `0`, and immediately resets the TextBox content to `maxEligible`.
- **Fix Required**: Remove the programmatic overwrite from `RecalculateTotals()` on input text change; only set the default loan amount once upon initial scheme/customer load or when the user clicks an explicit "Apply Max LTV" button.

### 2.3 Broken Customer Picker in New Loan Entry
- **Path**: `NewLoanEntryControl` `OnPickCustomerClick` → `_customerService.SearchCustomersAsync("", 1)` → first customer picked.
- **Root Cause**: The picker button does not launch a customer lookup modal. It blindly selects `customers[0]`. If a store has hundreds of customers, staff cannot select any customer other than the first one unless they manually remember the ID.
- **Fix Required**: Introduce a clean `CustomerLookupDialog` with live type-to-search (Name, Mobile, Code, Aadhaar), DataGrid selection, keyboard Enter-to-select, and return the selected customer to `NewLoanEntryControl`.

### 2.4 Lack of Input Validation & Negative Amount Vulnerabilities
- **Path**: `TxtInterestAmount`, `TxtPartPrincipal`, `TxtPostalCharges`, `TxtAdjustment` → `decimal.TryParse` → `CreateReceiptDto` → `ReceiptService.ProcessReceiptAsync` → `receipts` & `voucher_lines`.
- **Root Cause**: `decimal.TryParse` successfully parses negative numbers (e.g., `-100.00`). No guard exists in UI or ViewModel to reject negative values. This allows users to post negative principal or interest, reversing cash balances and corrupting double-entry accounting.
- **Fix Required**:
  1. Implement centralized validation enforcing:
     - All financial amounts must be `>= 0.00`.
     - Weights must be `>= 0.000` with 3 decimal precision.
     - Phone numbers must be exactly 10 digits (`^\d{10}$`).
     - Aadhaar numbers must be exactly 12 digits (`^\d{12}$`).
     - PAN must match `^[A-Z]{5}[0-9]{4}[A-Z]$`.
     - Pincode must be exactly 6 digits (`^\d{6}$`).
  2. Display inline red error text under invalid fields.
  3. Disable Save/Disburse buttons until the form is completely valid (`INotifyDataErrorInfo`).

### 2.5 Hardcoded Function-Key Shortcut Labels
- **Path**: `MainWindow.xaml` and control XAML files.
- **Root Cause**: Menu titles include strings such as `_New Loan Entry (F2)`, `_Member Detail (F1)`, and buttons include `Save Customer (F10)`. The prompt mandates removing all function keys and replacing them with standard buttons and natural keyboard navigation (Tab/Shift+Tab, Enter, Esc).
- **Fix Required**: Strip all `(F1)`–`(F10)` suffixes from menu headers, button labels, and status bar text. Reorganize `MainWindow` into standard grouped sections (Loans, Receipts, Re-Pledge, Accounts, Reports, Masters, Settings).

### 2.6 Hardcoded User ID and Role in Transactions
- **Path**: `NewLoanEntryControl.OnIssueLoanClick` / `LoanReceiptControl.OnSaveReceiptClick` → `CreateLoanDto` / `CreateReceiptDto`.
- **Root Cause**: Both screens pass `Guid.Empty` for `UserId` and `UserRole.Owner` hardcoded, ignoring the logged-in user in `MainWindow`.
- **Fix Required**: Pass the active authenticated `User` from `MainWindow` into each child control constructor and utilize the real `User.Id` and `User.Role`.

---

## 3. Screen-by-Screen Remediation Plan

### Screen 1: Application Shell (`MainWindow.xaml` / `MainWindow.xaml.cs`)
- [ ] Remove all `(F1)`–`(F10)` shortcut labels from menu items.
- [ ] Group menu navigation into standard sections: Loans, Receipts, Re-Pledge, Accounts, Reports, Masters, Settings.
- [ ] Modernize brand header and status bar.
- [ ] Enforce global key handling: Enter moves to next field, Esc cancels/closes modal dialogs.

### Screen 2: Global Styles & Typography (`Themes/Colors.xaml`, `Fonts.xaml`, `Controls.xaml`)
- [ ] Create `Colors.xaml`: Deep blue primary (`#1E3A8A`), neutral gray background (`#F8FAFC`), white cards (`#FFFFFF`), borders (`#E2E8F0`), error red (`#DC2626`), success green (`#16A34A`).
- [ ] Create `Fonts.xaml`: Segoe UI base font with Noto Sans Tamil fallback, 14px body, 13px semi-bold labels, 20px headers.
- [ ] Create `Controls.xaml`: Standard 36px height for TextBoxes, ComboBoxes, and DatePickers; consistent button styles (Primary, Secondary, Danger) with hover/focus states; DataGrid styles with alternating rows and sticky headers.
- [ ] Merge into `App.xaml`.

### Screen 3: Customer Master (`CustomerMasterControl`)
- [ ] Replace `(F10)` button with clean "Save Customer" and "Cancel".
- [ ] Implement 10-digit mobile validation, 12-digit Aadhaar validation, 6-digit Pincode validation.
- [ ] Add inline validation error messages.
- [ ] Integrate server-backed Number Series for customer code generation.
- [ ] Store customer photos in `C:\ProgramData\JGF\CustomerPhotos`.

### Screen 4: New Loan Entry (`NewLoanEntryControl`)
- [ ] Implement `INotifyPropertyChanged` on `LoanItemRowViewModel`.
- [ ] Ensure Net Weight auto-calculates as `GrossWeight - DeductionWeight` (3 decimal places).
- [ ] Remove auto-overwrite bug in `TxtLoanAmount`.
- [ ] Create and wire up interactive `CustomerLookupDialog`.
- [ ] Enforce scheme maximum LTV validation and require Owner override reason if exceeded.
- [ ] Pass current authenticated user to `CreateLoanDto`.
- [ ] Replace `(F10)` button label with "Disburse Loan & Print".

### Screen 5: Loan Receipts (`LoanReceiptControl`)
- [ ] Reject negative input amounts across all payment fields.
- [ ] Enforce Part Principal cannot exceed outstanding principal balance.
- [ ] Format all amounts to 2 decimal places with thousands separators (`₹ 1,234.56`).
- [ ] Pass current authenticated user to `CreateReceiptDto`.
- [ ] Replace `(F10)` button label with "Process Receipt & Print".

### Screen 6: Bank Re-Pledge (`BankRepledgeControl`, `BankRepledgeNewEntryControl`, `BankPaymentControl`)
- [ ] Enforce positive numbers on Bank Interest Rate, Appraised Weight, and Disbursed Amount.
- [ ] Enforce Maturity Date > Pledging Date.
- [ ] Format gold weights to 3 decimals (`0.000 g`).
- [ ] Remove `(F10)` button labels and modernize tab styles.

### Screen 7: Overdue Notices & Auction (`NoticeAuctionControl`)
- [ ] Enforce positive postal charges and grace days.
- [ ] Enforce statutory 15-day pre-notice rule for auction scheduling.
- [ ] Format currency and weight columns in DataGrids.

### Screen 8: Accounts & Day Book (`DayBookEntryControl`, `DayEndCloseControl`, `AccountsIntegrityControl`)
- [ ] Reject negative manual voucher amounts.
- [ ] Reject negative drawer cash counts in Day-End Close.
- [ ] Right-align all currency columns in Day Book and Financial Reports DataGrids.
- [ ] Verify Accounts Integrity Auditor executes with 0 errors.

### Screen 9: Masters (`SchemeMasterControl`, `GoldRateMasterControl`, `JewelTypeMasterControl`, `BankMasterControl`, `LedgerHeadMasterControl`, `NumberSeriesMasterControl`)
- [ ] Enforce positive rates and limits across all master screens.
- [ ] Remove `(F10)` from Gold Rate and Scheme save buttons.
- [ ] Add try/catch error handling and inline validation.

---

## 4. Verification & Testing Checklist
- [ ] Execute automated unit tests for all calculations and validation rules (`dotnet test`).
- [ ] Run automated end-to-end integration flow.
- [ ] Verify Accounts Integrity Audit returns PASS across all 6 financial checks.
- [ ] Compile self-contained Release build and verified Inno Setup installers.
- [ ] Produce `TESTLOG.md` documenting every tested screen and field.
- [ ] Produce manual 20-click verification checklist for the user.

