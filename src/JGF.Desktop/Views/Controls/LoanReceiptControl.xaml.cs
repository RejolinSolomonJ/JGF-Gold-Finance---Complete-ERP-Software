using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JGF.Application.Loans;
using JGF.Application.Receipts;
using JGF.Application.Reports;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Win32;
using Serilog;

namespace JGF.Desktop.Views.Controls;

public class CustomerLoanBalanceViewModel
{
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public DateOnly PledgeDate { get; set; }
    public string SchemeInfo { get; set; } = string.Empty;
    public string JewelDetails { get; set; } = string.Empty;
    public decimal OriginalPrincipal { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal MonthlyInterest { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal PayToCloseNow { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public partial class LoanReceiptControl : UserControl
{
    private readonly ILoanService _loanService;
    private readonly IReceiptService _receiptService;
    private readonly IInterestCalculator _interestCalculator;
    private readonly User? _currentUser;
    private readonly ICustomerService? _customerService;
    private readonly IJlReportService? _reportService;

    private Loan? _loadedLoan;
    private decimal _currentPrincipalBalance = 0m;
    private decimal _monthlyInterestDue = 0m;
    private InterestCalculationResult? _calcResult;
    private bool _isUpdatingReceiptType = false;
    private bool _isPromptingClosure = false;
    private bool _isInitialized = false;
    private Receipt? _lastReceipt;
    private List<Receipt> _pastReceipts = new();

    // Customer Tracker State
    private Customer? _currentTrackerCustomer;
    private List<CustomerLoanBalanceViewModel> _trackerLoanViewModels = new();

    public LoanReceiptControl(
        ILoanService loanService,
        IReceiptService receiptService,
        IInterestCalculator interestCalculator,
        User? currentUser = null,
        ICustomerService? customerService = null,
        IJlReportService? reportService = null)
    {
        InitializeComponent();
        _loanService = loanService;
        _receiptService = receiptService;
        _interestCalculator = interestCalculator;
        _currentUser = currentUser;
        _customerService = customerService;
        _reportService = reportService;

        _isInitialized = true;
        DpReceiptDate.SelectedDate = DateTime.Today;
        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(TxtSearchQuery.Text))
        {
            await LoadLoanAsync(TxtSearchQuery.Text.Trim());
        }
    }

    // ==========================================
    // TAB 1: LOAN RECEIPT & PAY TO CLOSE LOGIC
    // ==========================================

    private async void OnSearchLoanClick(object sender, RoutedEventArgs e)
    {
        await LoadLoanAsync(TxtSearchQuery.Text.Trim());
    }

    private async void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await LoadLoanAsync(TxtSearchQuery.Text.Trim());
        }
    }

    public async Task LoadLoanAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            MessageBox.Show("Please enter a Loan Number or Customer Mobile.", "Search Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var loan = await _loanService.FindLoanByNumberOrBarcodeAsync(query);
            if (loan == null)
            {
                // Try searching by mobile or customer code
                var loans = await _loanService.SearchLoansAsync(query, take: 1);
                loan = loans.FirstOrDefault();
            }

            if (loan == null)
            {
                MessageBox.Show($"No active loan found for query: '{query}'", "Loan Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _loadedLoan = loan;
            var history = await _loanService.GetLoanHistoryAsync(loan.Id);
            _currentPrincipalBalance = history.OrderByDescending(h => h.EntryDate).ThenByDescending(h => h.CreatedAtUtc).FirstOrDefault()?.Balance ?? loan.LoanAmount;
            if (_currentPrincipalBalance <= 0 && loan.Status == LoanStatus.Active)
            {
                _currentPrincipalBalance = loan.LoanAmount;
            }

            _pastReceipts = await _receiptService.GetReceiptsForLoanAsync(loan.Id);
            _lastReceipt = _pastReceipts.FirstOrDefault();
            BtnPastReceipts.IsEnabled = _pastReceipts.Count > 0;
            BtnDirectPrint.IsEnabled = _lastReceipt != null;
            BtnDownloadPdf.IsEnabled = _lastReceipt != null;

            DisplayLoanInfo(loan);
            RecalculateInterest();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load loan for receipt.");
            MessageBox.Show($"Failed to load loan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DisplayLoanInfo(Loan loan)
    {
        TxtCustomerName.Text = loan.Customer?.FullName ?? "-";
        TxtCustomerMobile.Text = loan.Customer?.MobileNumber ?? "-";
        TxtLoanNo.Text = $"{loan.LoanNumber} ({loan.Status})";
        TxtSchemeInfo.Text = $"{loan.Scheme?.Name} @ {loan.Scheme?.InterestRatePerMonth:F2}% / mo";
        TxtPledgeDate.Text = ValidationHelper.FormatDate(loan.PledgeDate);
        TxtLastPaid.Text = loan.LastInterestPaidDate.HasValue 
            ? $"Last Paid: {ValidationHelper.FormatDate(loan.LastInterestPaidDate.Value)}" 
            : "No payments yet";
        TxtPrincipalBalance.Text = ValidationHelper.FormatCurrency(_currentPrincipalBalance);
        TxtNetWeight.Text = $"Net Wt: {ValidationHelper.FormatWeight(loan.NetWeight)}";

        // Pay to Close Now golden card update
        var accrued = _calcResult?.TotalInterest ?? 0m;
        var payToClose = _currentPrincipalBalance + accrued;
        TxtPayToCloseNow.Text = ValidationHelper.FormatCurrency(payToClose);
        TxtPayToCloseBreakdown.Text = $"Principal: {ValidationHelper.FormatCurrency(_currentPrincipalBalance)} + Interest: {ValidationHelper.FormatCurrency(accrued)}";

        UpdatePostPaymentBalanceNotice();
    }

    private void OnReceiptDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        RecalculateInterest();
    }

    private void RecalculateInterest()
    {
        if (!_isInitialized || _loadedLoan == null) return;

        var receiptDate = DateOnly.FromDateTime(DpReceiptDate.SelectedDate ?? DateTime.Today);
        if (receiptDate < _loadedLoan.PledgeDate)
        {
            MessageBox.Show("Receipt date cannot be earlier than the loan pledge date.", "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
            DpReceiptDate.SelectedDate = _loadedLoan.PledgeDate.ToDateTime(TimeOnly.MinValue);
            return;
        }

        var scheme = _loadedLoan.Scheme ?? new Scheme
        {
            InterestRatePerMonth = 1.5m,
            PenalInterestRatePerMonth = 0.5m,
            MaxTenureMonths = 12,
            GraceDaysAfterExpiry = 7,
            MinimumInterestDays = 30
        };

        var principal = _currentPrincipalBalance > 0 ? _currentPrincipalBalance : _loadedLoan.LoanAmount;
        _monthlyInterestDue = Math.Round(principal * (scheme.InterestRatePerMonth / 100m), 2, MidpointRounding.AwayFromZero);

        _calcResult = _interestCalculator.CalculateInterest(
            principal,
            scheme.InterestRatePerMonth,
            scheme.PenalInterestRatePerMonth,
            _loadedLoan.PledgeDate,
            receiptDate,
            _loadedLoan.LastInterestPaidDate,
            scheme.MaxTenureMonths,
            scheme.GraceDaysAfterExpiry,
            scheme.MinimumInterestDays,
            false);

        GridInterest.ItemsSource = _calcResult.Breakdown;

        // Auto Calculation Date Badge
        var startDate = _loadedLoan.LastInterestPaidDate ?? _loadedLoan.PledgeDate;
        var elapsedDays = Math.Max(0, receiptDate.DayNumber - startDate.DayNumber);
        if (TxtAutoCalcDateBadge != null)
        {
            TxtAutoCalcDateBadge.Text = $"🗓️ Auto Calculated for {receiptDate:dd-MMM-yyyy} ({elapsedDays} days / {_calcResult.TotalMonthsCharged:F2} mos)";
        }

        // This Month Due Card
        if (TxtThisMonthDue != null)
        {
            TxtThisMonthDue.Text = $"{ValidationHelper.FormatCurrency(_monthlyInterestDue)} / mo";
        }
        if (TxtAccruedInterestSubtext != null)
        {
            TxtAccruedInterestSubtext.Text = $"Accrued to Date: {ValidationHelper.FormatCurrency(_calcResult.TotalInterest)} ({_calcResult.TotalDays} days)";
        }

        // Quick Fill Buttons in Payment Form
        if (BtnQuickMonthDue != null)
        {
            BtnQuickMonthDue.Content = $"⚡ 1 Mo: {ValidationHelper.FormatCurrency(_monthlyInterestDue)}";
        }
        if (BtnQuickFullInterest != null)
        {
            BtnQuickFullInterest.Content = $"⚡ Full: {ValidationHelper.FormatCurrency(_calcResult.TotalInterest)}";
        }
        if (TxtInterestHelperRate != null)
        {
            TxtInterestHelperRate.Text = $"(@ {scheme.InterestRatePerMonth:F2}%/mo = {ValidationHelper.FormatCurrency(_monthlyInterestDue)}/mo)";
        }

        // Update golden Pay to Close Now card
        var payToClose = _currentPrincipalBalance + _calcResult.TotalInterest;
        TxtPayToCloseNow.Text = ValidationHelper.FormatCurrency(payToClose);
        TxtPayToCloseBreakdown.Text = $"Principal: {ValidationHelper.FormatCurrency(_currentPrincipalBalance)} + Interest: {ValidationHelper.FormatCurrency(_calcResult.TotalInterest)}";

        ApplyReceiptTypeRules();
        UpdateTotalReceivable();
        UpdatePostPaymentBalanceNotice();
    }

    private void OnQuick1MonthDueClick(object sender, RoutedEventArgs e)
    {
        if (_loadedLoan == null) return;
        TxtInterestAmount.Text = _monthlyInterestDue.ToString("F2");
        UpdateTotalReceivable();
    }

    private void OnQuickFullAccruedInterestClick(object sender, RoutedEventArgs e)
    {
        if (_loadedLoan == null || _calcResult == null) return;
        TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");
        UpdateTotalReceivable();
    }

    private void OnSettleCloseNowClick(object sender, RoutedEventArgs e)
    {
        if (_loadedLoan == null || _calcResult == null)
        {
            MessageBox.Show("Please search and load an active loan first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CmbReceiptType.SelectedIndex = 3; // Full Closure & Release
        ApplyReceiptTypeRules();
        UpdateTotalReceivable();
        UpdatePostPaymentBalanceNotice();

        TxtPartPrincipal.Focus();
    }

    private void OnReceiptTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyReceiptTypeRules();
        UpdateTotalReceivable();
        UpdatePostPaymentBalanceNotice();
    }

    private void ApplyReceiptTypeRules()
    {
        if (_loadedLoan == null || _calcResult == null) return;

        _isUpdatingReceiptType = true;
        try
        {
            switch (CmbReceiptType.SelectedIndex)
            {
                case 0: // Interest Only - Full Accrued
                    TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");
                    TxtInterestAmount.IsReadOnly = false;
                    TxtPartPrincipal.Text = "0.00";
                    TxtPartPrincipal.IsReadOnly = true;
                    break;

                case 1: // Interest Only - 1 Month Due
                    TxtInterestAmount.Text = _monthlyInterestDue.ToString("F2");
                    TxtInterestAmount.IsReadOnly = false;
                    TxtPartPrincipal.Text = "0.00";
                    TxtPartPrincipal.IsReadOnly = true;
                    break;

                case 2: // Part Payment
                    TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");
                    TxtInterestAmount.IsReadOnly = false;
                    if (decimal.TryParse(TxtPartPrincipal.Text, out var currPart) && currPart == _currentPrincipalBalance)
                    {
                        TxtPartPrincipal.Text = "0.00";
                    }
                    TxtPartPrincipal.IsReadOnly = false;
                    break;

                case 3: // Full Closure
                    TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");
                    TxtInterestAmount.IsReadOnly = false;
                    TxtPartPrincipal.Text = _currentPrincipalBalance.ToString("F2");
                    TxtPartPrincipal.IsReadOnly = true;
                    break;
            }
        }
        finally
        {
            _isUpdatingReceiptType = false;
        }
    }

    private void OnPaymentAmountChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingReceiptType) return;

        // Automatic prompt to switch to Full Closure if user enters full principal in Part Payment mode
        if (!_isPromptingClosure && CmbReceiptType.SelectedIndex == 2 && _loadedLoan != null && _currentPrincipalBalance > 0)
        {
            if (decimal.TryParse(TxtPartPrincipal.Text, out var enteredPart) && enteredPart >= _currentPrincipalBalance)
            {
                _isPromptingClosure = true;
                var result = MessageBox.Show(
                    $"The entered principal amount ({ValidationHelper.FormatCurrency(enteredPart)}) completely settles the remaining loan balance of {ValidationHelper.FormatCurrency(_currentPrincipalBalance)}.\n\n" +
                    "Would you like to automatically switch to 'Full Closure & Release' to close this loan and generate the Jewel Release Slip?",
                    "Full Principal Payment Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    CmbReceiptType.SelectedIndex = 3; // Full Closure
                    ApplyReceiptTypeRules();
                }
                _isPromptingClosure = false;
            }
        }

        UpdateTotalReceivable();
        UpdatePostPaymentBalanceNotice();
    }

    private void UpdatePostPaymentBalanceNotice()
    {
        if (!_isInitialized || TxtPostPaymentBalance == null || TxtAutoCloseHint == null || BdrBalanceNotice == null) return;

        if (_loadedLoan == null)
        {
            TxtPostPaymentBalance.Text = "📌 Load a loan to inspect balance and settlement breakdown.";
            TxtAutoCloseHint.Text = string.Empty;
            return;
        }

        decimal.TryParse(TxtPartPrincipal?.Text, out var partPrin);
        decimal remaining = _currentPrincipalBalance;

        switch (CmbReceiptType?.SelectedIndex ?? 0)
        {
            case 0: // Interest Only - Full Accrued
                TxtPostPaymentBalance.Text = $"📌 Remaining Balance: {ValidationHelper.FormatCurrency(_currentPrincipalBalance)} (Full Accrued Interest Settled)";
                TxtAutoCloseHint.Text = "💡 All past accrued interest cleared. Loan stays active.";
                BdrBalanceNotice.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF9, 0xFF));
                BdrBalanceNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBA, 0xE6, 0xFD));
                TxtPostPaymentBalance.Foreground = new SolidColorBrush(Color.FromRgb(0x03, 0x69, 0xA1));
                break;

            case 1: // Interest Only - 1 Month Due
                TxtPostPaymentBalance.Text = $"📌 Remaining Balance: {ValidationHelper.FormatCurrency(_currentPrincipalBalance)} (1 Month Interest Settled: {ValidationHelper.FormatCurrency(_monthlyInterestDue)})";
                TxtAutoCloseHint.Text = "💡 Current month interest collected. Loan stays active.";
                BdrBalanceNotice.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF9, 0xFF));
                BdrBalanceNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBA, 0xE6, 0xFD));
                TxtPostPaymentBalance.Foreground = new SolidColorBrush(Color.FromRgb(0x03, 0x69, 0xA1));
                break;

            case 2: // Part Payment
                remaining = Math.Max(0m, _currentPrincipalBalance - partPrin);
                TxtPostPaymentBalance.Text = $"📌 Remaining Balance: {ValidationHelper.FormatCurrency(remaining)} (After paying {ValidationHelper.FormatCurrency(partPrin)} principal)";
                if (remaining == 0m && _currentPrincipalBalance > 0m)
                {
                    TxtAutoCloseHint.Text = "⚡ FULL BALANCE COVERED! Select 'Full Closure & Release' to complete settlement.";
                    BdrBalanceNotice.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
                    BdrBalanceNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFD, 0xE6, 0x8A));
                    TxtPostPaymentBalance.Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09));
                }
                else
                {
                    TxtAutoCloseHint.Text = $"💡 Principal will decrease from {ValidationHelper.FormatCurrency(_currentPrincipalBalance)} to {ValidationHelper.FormatCurrency(remaining)}";
                    BdrBalanceNotice.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xF4));
                    BdrBalanceNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBB, 0xF7, 0xD0));
                    TxtPostPaymentBalance.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34));
                }
                break;

            case 3: // Full Closure
                TxtPostPaymentBalance.Text = "📌 Remaining Balance: ₹ 0.00 (FULL CLOSURE & JEWEL RELEASE)";
                TxtAutoCloseHint.Text = "🎉 Loan will be closed and collateral jewel release document printed!";
                BdrBalanceNotice.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xF4));
                BdrBalanceNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBB, 0xF7, 0xD0));
                TxtPostPaymentBalance.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34));
                break;
        }
    }

    private bool UpdateTotalReceivable()
    {
        if (!_isInitialized || TxtTotalReceivable == null || TxtInterestError == null || TxtPrincipalError == null || TxtPostalError == null || TxtAdjustmentError == null || BtnProcessReceipt == null) return false;

        bool isValid = true;

        // Interest
        if (!decimal.TryParse(TxtInterestAmount.Text, out var interest) || interest < 0)
        {
            TxtInterestError.Visibility = Visibility.Visible;
            isValid = false;
        }
        else
        {
            TxtInterestError.Visibility = Visibility.Collapsed;
        }

        // Principal
        if (!decimal.TryParse(TxtPartPrincipal.Text, out var principal) || principal < 0)
        {
            TxtPrincipalError.Text = "Principal cannot be negative.";
            TxtPrincipalError.Visibility = Visibility.Visible;
            isValid = false;
        }
        else if (_loadedLoan != null && principal > _currentPrincipalBalance)
        {
            TxtPrincipalError.Text = $"Cannot exceed balance {ValidationHelper.FormatCurrency(_currentPrincipalBalance)}.";
            TxtPrincipalError.Visibility = Visibility.Visible;
            isValid = false;
        }
        else
        {
            TxtPrincipalError.Visibility = Visibility.Collapsed;
        }

        // Postal charges
        if (!decimal.TryParse(TxtPostalCharges.Text, out var postal) || postal < 0)
        {
            TxtPostalError.Visibility = Visibility.Visible;
            isValid = false;
        }
        else
        {
            TxtPostalError.Visibility = Visibility.Collapsed;
        }

        // Adjustment
        if (!decimal.TryParse(TxtAdjustment.Text, out var adj) || adj < 0)
        {
            TxtAdjustmentError.Visibility = Visibility.Visible;
            isValid = false;
        }
        else
        {
            TxtAdjustmentError.Visibility = Visibility.Collapsed;
        }

        var total = Math.Max(0m, interest + principal + postal - adj);
        TxtTotalReceivable.Text = ValidationHelper.FormatCurrency(total);

        if (_loadedLoan == null || total <= 0)
        {
            isValid = false;
        }

        BtnProcessReceipt.IsEnabled = isValid;
        return isValid;
    }

    private async void OnProcessReceiptClick(object sender, RoutedEventArgs e)
    {
        if (_loadedLoan == null || _calcResult == null)
        {
            MessageBox.Show("Please load an active loan first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!UpdateTotalReceivable())
        {
            MessageBox.Show("Please ensure all payment amounts are valid and total receivable is greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtInterestAmount.Text, out var interest);
        decimal.TryParse(TxtPartPrincipal.Text, out var principal);
        decimal.TryParse(TxtPostalCharges.Text, out var postal);
        decimal.TryParse(TxtAdjustment.Text, out var adj);

        var rType = CmbReceiptType.SelectedIndex switch
        {
            0 => ReceiptType.InterestOnly,
            1 => ReceiptType.InterestOnly,
            2 => ReceiptType.PartPrincipal,
            3 => ReceiptType.Closure,
            _ => ReceiptType.InterestOnly
        };

        // Safety verification if user is paying full principal in non-closure receipt
        if (rType != ReceiptType.Closure && principal >= _currentPrincipalBalance && _currentPrincipalBalance > 0)
        {
            var prompt = MessageBox.Show(
                $"The principal payment of {ValidationHelper.FormatCurrency(principal)} covers the entire remaining balance.\n\n" +
                "Do you want to process this as a 'Full Closure & Release' instead so jewels can be officially released to the customer?",
                "Complete Closure Confirmation",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (prompt == MessageBoxResult.Cancel) return;
            if (prompt == MessageBoxResult.Yes)
            {
                rType = ReceiptType.Closure;
                CmbReceiptType.SelectedIndex = 3;
                principal = _currentPrincipalBalance;
            }
        }

        var payMode = CmbPayMode.SelectedIndex switch
        {
            1 => PaymentMode.UPI,
            2 => PaymentMode.BankTransfer,
            3 => PaymentMode.Cheque,
            _ => PaymentMode.Cash
        };

        var receiptDate = DateOnly.FromDateTime(DpReceiptDate.SelectedDate ?? DateTime.Today);
        var userId = _currentUser?.Id ?? Guid.Empty;
        var userRole = _currentUser?.Role ?? UserRole.Owner;

        var dto = new CreateReceiptDto(
            _loadedLoan.Id,
            receiptDate,
            rType,
            payMode,
            principal,
            interest,
            _calcResult.PenalInterest,
            postal,
            0m,
            null,
            adj,
            adj > 0 ? "Rounding adjustment" : null,
            null,
            _calcResult.Breakdown.FirstOrDefault()?.FromDate ?? receiptDate,
            receiptDate,
            _calcResult.TotalDays,
            userId,
            userRole
        );

        try
        {
            BtnProcessReceipt.IsEnabled = false;
            var receipt = await _receiptService.ProcessReceiptAsync(dto);

            _lastReceipt = receipt;
            _pastReceipts.Insert(0, receipt);
            BtnPastReceipts.IsEnabled = true;
            BtnDirectPrint.IsEnabled = true;
            BtnDownloadPdf.IsEnabled = true;

            // Open Receipt Viewer Dialog with live preview, direct print, and download PDF
            var parentWin = Window.GetWindow(this);
            var dlg = new ReceiptActionsDialog(receipt, _loadedLoan, _loadedLoan.Customer, _receiptService)
            {
                Owner = parentWin
            };
            dlg.ShowDialog();

            // Reload loan status and balances
            _loadedLoan = await _loanService.GetLoanByIdAsync(_loadedLoan.Id);
            if (_loadedLoan != null)
            {
                var history = await _loanService.GetLoanHistoryAsync(_loadedLoan.Id);
                _currentPrincipalBalance = history.OrderByDescending(h => h.EntryDate).ThenByDescending(h => h.CreatedAtUtc).FirstOrDefault()?.Balance ?? 0m;
                DisplayLoanInfo(_loadedLoan);
            }
            RecalculateInterest();

            // Refresh Tracker if this customer is currently selected in Tab 2
            if (_currentTrackerCustomer != null && _loadedLoan != null && _currentTrackerCustomer.Id == _loadedLoan.CustomerId)
            {
                await LoadCustomerTrackerDataAsync(_currentTrackerCustomer);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Receipt processing failed.");
            MessageBox.Show($"Receipt processing failed: {ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnProcessReceipt.IsEnabled = true;
        }
    }

    private void OnDirectPrintClick(object sender, RoutedEventArgs e)
    {
        if (_lastReceipt == null || _loadedLoan == null)
        {
            MessageBox.Show("No receipt available to print.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ReceiptPrintHelper.PrintReceipt(_lastReceipt, _loadedLoan, _loadedLoan.Customer, Window.GetWindow(this));
    }

    private async void OnDownloadPdfClick(object sender, RoutedEventArgs e)
    {
        if (_lastReceipt == null)
        {
            MessageBox.Show("No receipt available to download.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await ReceiptPrintHelper.DownloadReceiptPdfAsync(_receiptService, _lastReceipt.Id, _lastReceipt.ReceiptNumber, Window.GetWindow(this));
    }

    private void OnPastReceiptsClick(object sender, RoutedEventArgs e)
    {
        if (_loadedLoan == null)
        {
            MessageBox.Show("Please load a loan first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_pastReceipts.Count == 0)
        {
            MessageBox.Show("No previous receipts found for this loan.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new PastReceiptsDialog(_loadedLoan, _loadedLoan.Customer, _pastReceipts, _receiptService)
        {
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();
    }

    // ==========================================
    // TAB 2: CUSTOMER BALANCE TRACKER LOGIC
    // ==========================================

    private async void OnCustomerTrackerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SearchAndLoadCustomerTrackerAsync(TxtCustomerTrackerSearch.Text.Trim());
        }
    }

    private async void OnCustomerTrackerSearchClick(object sender, RoutedEventArgs e)
    {
        await SearchAndLoadCustomerTrackerAsync(TxtCustomerTrackerSearch.Text.Trim());
    }

    private async void OnCustomerTrackerPickClick(object sender, RoutedEventArgs e)
    {
        if (_customerService == null)
        {
            MessageBox.Show("Customer service is not available.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new CustomerLookupDialog(_customerService, TxtCustomerTrackerSearch.Text.Trim())
        {
            Owner = Window.GetWindow(this)
        };

        if (dlg.ShowDialog() == true && dlg.SelectedCustomer != null)
        {
            TxtCustomerTrackerSearch.Text = dlg.SelectedCustomer.FullName;
            await LoadCustomerTrackerDataAsync(dlg.SelectedCustomer);
        }
    }

    private async void OnCustomerTrackerRefreshClick(object sender, RoutedEventArgs e)
    {
        if (_currentTrackerCustomer != null)
        {
            await LoadCustomerTrackerDataAsync(_currentTrackerCustomer);
        }
        else if (!string.IsNullOrWhiteSpace(TxtCustomerTrackerSearch.Text))
        {
            await SearchAndLoadCustomerTrackerAsync(TxtCustomerTrackerSearch.Text.Trim());
        }
    }

    private async Task SearchAndLoadCustomerTrackerAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            MessageBox.Show("Please enter customer name, mobile, or customer code to search.", "Search Query Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Customer? customer = null;
            if (_customerService != null)
            {
                var customers = await _customerService.SearchCustomersAsync(query, take: 5);
                customer = customers.FirstOrDefault();
            }

            if (customer == null)
            {
                // Fallback search through loans
                var loans = await _loanService.SearchLoansAsync(query, take: 1);
                customer = loans.FirstOrDefault()?.Customer;
            }

            if (customer == null)
            {
                MessageBox.Show($"No customer found matching '{query}'.", "Customer Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadCustomerTrackerDataAsync(customer);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to search customer for tracker.");
            MessageBox.Show($"Failed to search customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadCustomerTrackerDataAsync(Customer customer)
    {
        try
        {
            _currentTrackerCustomer = customer;
            TxtTrackerCustName.Text = $"{customer.FullName} ({customer.Code})";
            TxtTrackerCustPhone.Text = $"Mobile: {customer.MobileNumber}";
            TxtTrackerCustAddress.Text = $"Address: {customer.Address ?? "-"}, {customer.City ?? "-"}";

            // Find all loans for this customer
            var allLoans = await _loanService.SearchLoansAsync(customer.MobileNumber, take: 100);
            if (allLoans.Count == 0 && !string.IsNullOrWhiteSpace(customer.Code))
            {
                allLoans = await _loanService.SearchLoansAsync(customer.Code, take: 100);
            }

            var viewModels = new List<CustomerLoanBalanceViewModel>();
            var today = DateOnly.FromDateTime(DateTime.Today);

            foreach (var loan in allLoans)
            {
                // Compute current balance from ledger history
                var history = await _loanService.GetLoanHistoryAsync(loan.Id);
                var balance = history.OrderByDescending(h => h.EntryDate).ThenByDescending(h => h.CreatedAtUtc).FirstOrDefault()?.Balance ?? loan.LoanAmount;
                if (balance <= 0 && loan.Status == LoanStatus.Active) balance = loan.LoanAmount;

                var scheme = loan.Scheme ?? new Scheme
                {
                    InterestRatePerMonth = 1.5m,
                    PenalInterestRatePerMonth = 0.5m,
                    MaxTenureMonths = 12,
                    GraceDaysAfterExpiry = 7,
                    MinimumInterestDays = 30
                };

                decimal monthlyInterest = 0m;
                decimal accruedInterest = 0m;
                decimal payToClose = 0m;

                if (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.PartiallyPaid)
                {
                    monthlyInterest = Math.Round(balance * (scheme.InterestRatePerMonth / 100m), 2, MidpointRounding.AwayFromZero);

                    var calc = _interestCalculator.CalculateInterest(
                        balance,
                        scheme.InterestRatePerMonth,
                        scheme.PenalInterestRatePerMonth,
                        loan.PledgeDate,
                        today,
                        loan.LastInterestPaidDate,
                        scheme.MaxTenureMonths,
                        scheme.GraceDaysAfterExpiry,
                        scheme.MinimumInterestDays,
                        false);

                    accruedInterest = calc.TotalInterest;
                    payToClose = balance + accruedInterest;
                }

                var jewelDesc = loan.Items != null && loan.Items.Count > 0
                    ? string.Join(", ", loan.Items.Select(i => $"{i.JewelType?.Name ?? "Item"} ({i.Quantity} pcs, {i.NetWeight:F3}g)"))
                    : $"{loan.NetWeight:F3}g Gold";

                viewModels.Add(new CustomerLoanBalanceViewModel
                {
                    LoanId = loan.Id,
                    LoanNumber = loan.LoanNumber,
                    PledgeDate = loan.PledgeDate,
                    SchemeInfo = $"{scheme.Name} ({scheme.InterestRatePerMonth:F2}%/mo)",
                    JewelDetails = jewelDesc,
                    OriginalPrincipal = loan.LoanAmount,
                    PrincipalPaid = Math.Max(0m, loan.LoanAmount - balance),
                    RemainingBalance = balance,
                    MonthlyInterest = monthlyInterest,
                    AccruedInterest = accruedInterest,
                    PayToCloseNow = payToClose,
                    Status = loan.Status.ToString(),
                    IsActive = (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.PartiallyPaid)
                });
            }

            _trackerLoanViewModels = viewModels;
            GridCustomerLoans.ItemsSource = _trackerLoanViewModels;

            // Aggregate KPI Cards
            var activeItems = _trackerLoanViewModels.Where(v => v.IsActive).ToList();
            var activeLoans = allLoans.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid).ToList();

            TxtTrackerActiveLoansCount.Text = $"{activeItems.Count} {(activeItems.Count == 1 ? "Active Loan" : "Active Loans")}";
            TxtTrackerTotalPledgedWeight.Text = $"{activeLoans.Sum(l => l.NetWeight):F3} g Gold Pledged";
            TxtTrackerTotalDisbursed.Text = ValidationHelper.FormatCurrency(allLoans.Sum(l => l.LoanAmount));
            TxtTrackerRemainingPrincipal.Text = ValidationHelper.FormatCurrency(activeItems.Sum(i => i.RemainingBalance));
            if (TxtTrackerMonthlyInterest != null)
            {
                TxtTrackerMonthlyInterest.Text = $"{ValidationHelper.FormatCurrency(activeItems.Sum(i => i.MonthlyInterest))} / mo";
            }
            TxtTrackerAccruedInterest.Text = $"Accrued: {ValidationHelper.FormatCurrency(activeItems.Sum(i => i.AccruedInterest))}";
            TxtTrackerGrandPayToClose.Text = ValidationHelper.FormatCurrency(activeItems.Sum(i => i.PayToCloseNow));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load customer tracker data.");
            MessageBox.Show($"Failed to load customer loans: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnSettleCustomerLoanRowClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CustomerLoanBalanceViewModel item)
        {
            // Switch to Tab 1 (Issue Receipt & Pay to Close)
            ReceiptsTabControl.SelectedIndex = 0;

            // Load this specific loan
            TxtSearchQuery.Text = item.LoanNumber;
            await LoadLoanAsync(item.LoanNumber);
        }
    }

    private async void OnPrintCustomerStatementClick(object sender, RoutedEventArgs e)
    {
        if (_currentTrackerCustomer == null)
        {
            MessageBox.Show("Please search and select a customer first to print statement.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(GridCustomerLoans, $"JGF Statement - {_currentTrackerCustomer.FullName}");
                MessageBox.Show("Customer statement sent to printer successfully.", "Printed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print customer statement.");
            MessageBox.Show($"Failed to print statement: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnDownloadCustomerStatementClick(object sender, RoutedEventArgs e)
    {
        if (_currentTrackerCustomer == null)
        {
            MessageBox.Show("Please search and select a customer first to download statement.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var sfd = new SaveFileDialog
            {
                FileName = $"Statement_{_currentTrackerCustomer.Code}_{DateTime.Today:yyyyMMdd}.csv",
                Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
                Title = "Save Customer Statement"
            };

            if (sfd.ShowDialog() == true)
            {
                using var writer = new StreamWriter(sfd.FileName);
                await writer.WriteLineAsync($"JEBA GOLD FINANCE - CUSTOMER BALANCE STATEMENT");
                await writer.WriteLineAsync($"Customer: {_currentTrackerCustomer.FullName} ({_currentTrackerCustomer.Code})");
                await writer.WriteLineAsync($"Mobile: {_currentTrackerCustomer.MobileNumber}");
                await writer.WriteLineAsync($"Date: {DateTime.Now:dd-MMM-yyyy HH:mm}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("GL Number,Pledge Date,Scheme,Jewels,Original Principal,Principal Paid,Remaining Balance,Accrued Interest,Pay to Close Now,Status");

                foreach (var item in _trackerLoanViewModels)
                {
                    await writer.WriteLineAsync($"\"{item.LoanNumber}\",\"{item.PledgeDate:dd-MMM-yyyy}\",\"{item.SchemeInfo}\",\"{item.JewelDetails}\",{item.OriginalPrincipal:F2},{item.PrincipalPaid:F2},{item.RemainingBalance:F2},{item.AccruedInterest:F2},{item.PayToCloseNow:F2},\"{item.Status}\"");
                }

                await writer.WriteLineAsync();
                var active = _trackerLoanViewModels.Where(v => v.IsActive).ToList();
                await writer.WriteLineAsync($"Total Active Loans: {active.Count}");
                await writer.WriteLineAsync($"Total Remaining Balance: ₹ {active.Sum(a => a.RemainingBalance):N2}");
                await writer.WriteLineAsync($"Total Accrued Interest: ₹ {active.Sum(a => a.AccruedInterest):N2}");
                await writer.WriteLineAsync($"TOTAL TO PAY TO CLOSE ALL: ₹ {active.Sum(a => a.PayToCloseNow):N2}");

                MessageBox.Show($"Customer statement successfully saved to:\n{sfd.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download customer statement.");
            MessageBox.Show($"Failed to download statement: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
