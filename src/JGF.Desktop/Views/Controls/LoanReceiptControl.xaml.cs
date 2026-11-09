using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JGF.Application.Loans;
using JGF.Application.Receipts;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Serilog;

namespace JGF.Desktop.Views.Controls;

public partial class LoanReceiptControl : UserControl
{
    private readonly ILoanService _loanService;
    private readonly IReceiptService _receiptService;
    private readonly IInterestCalculator _interestCalculator;
    private readonly User? _currentUser;

    private Loan? _loadedLoan;
    private decimal _currentPrincipalBalance = 0m;
    private InterestCalculationResult? _calcResult;
    private bool _isUpdatingReceiptType = false;

    public LoanReceiptControl(
        ILoanService loanService,
        IReceiptService receiptService,
        IInterestCalculator interestCalculator,
        User? currentUser = null)
    {
        InitializeComponent();
        _loanService = loanService;
        _receiptService = receiptService;
        _interestCalculator = interestCalculator;
        _currentUser = currentUser;

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

    private async Task LoadLoanAsync(string query)
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
                // Try searching by mobile
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
            if (_currentPrincipalBalance <= 0) _currentPrincipalBalance = loan.LoanAmount;

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
    }

    private void OnReceiptDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        RecalculateInterest();
    }

    private void RecalculateInterest()
    {
        if (_loadedLoan == null) return;

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

        _calcResult = _interestCalculator.CalculateInterest(
            _currentPrincipalBalance > 0 ? _currentPrincipalBalance : _loadedLoan.LoanAmount,
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

        TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");

        ApplyReceiptTypeRules();
        UpdateTotalReceivable();
    }

    private void OnReceiptTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyReceiptTypeRules();
        UpdateTotalReceivable();
    }

    private void ApplyReceiptTypeRules()
    {
        if (_loadedLoan == null || _calcResult == null) return;

        _isUpdatingReceiptType = true;
        try
        {
            switch (CmbReceiptType.SelectedIndex)
            {
                case 0: // Interest Only
                    TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");
                    TxtInterestAmount.IsReadOnly = false;
                    TxtPartPrincipal.Text = "0.00";
                    TxtPartPrincipal.IsReadOnly = true;
                    break;

                case 1: // Part Payment
                    TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");
                    TxtInterestAmount.IsReadOnly = false;
                    TxtPartPrincipal.IsReadOnly = false;
                    break;

                case 2: // Full Closure
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
        if (_isUpdatingReceiptType) return;
        UpdateTotalReceivable();
    }

    private bool UpdateTotalReceivable()
    {
        if (TxtTotalReceivable == null) return false;

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

        var total = interest + principal + postal - adj;

        var rType = CmbReceiptType.SelectedIndex switch
        {
            0 => ReceiptType.InterestOnly,
            1 => ReceiptType.PartPrincipal,
            2 => ReceiptType.Closure,
            _ => ReceiptType.InterestOnly
        };

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

            if (rType == ReceiptType.Closure)
            {
                MessageBox.Show($"Loan {_loadedLoan.LoanNumber} is now FULLY CLOSED & REDEEMED!\n" +
                                $"Receipt #{receipt.ReceiptNumber} created.\n" +
                                $"Total Collected: {ValidationHelper.FormatCurrency(receipt.TotalAmount)}\n" +
                                $"Ready to inspect ornaments and print Release Slip (மீட்பு சீட்டு).",
                    "Loan Closed & Released", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Receipt #{receipt.ReceiptNumber} posted successfully!\n" +
                                $"Total Collected: {ValidationHelper.FormatCurrency(receipt.TotalAmount)}\n" +
                                $"Principal Paid: {ValidationHelper.FormatCurrency(receipt.PrincipalAmount)}\n" +
                                $"Interest Paid: {ValidationHelper.FormatCurrency(receipt.InterestAmount)}",
                    "Receipt Processed", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Reload loan status
            _loadedLoan = await _loanService.GetLoanByIdAsync(_loadedLoan.Id);
            if (_loadedLoan != null) DisplayLoanInfo(_loadedLoan);
            RecalculateInterest();
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
