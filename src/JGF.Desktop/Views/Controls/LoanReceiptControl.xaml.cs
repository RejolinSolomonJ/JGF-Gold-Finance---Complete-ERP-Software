using System.Windows;
using System.Windows.Controls;
using JGF.Application.Loans;
using JGF.Application.Receipts;
using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public partial class LoanReceiptControl : UserControl
{
    private readonly ILoanService _loanService;
    private readonly IReceiptService _receiptService;
    private readonly IInterestCalculator _interestCalculator;

    private Loan? _loadedLoan;
    private InterestCalculationResult? _calcResult;

    public LoanReceiptControl(
        ILoanService loanService,
        IReceiptService receiptService,
        IInterestCalculator interestCalculator)
    {
        InitializeComponent();
        _loanService = loanService;
        _receiptService = receiptService;
        _interestCalculator = interestCalculator;

        DpReceiptDate.SelectedDate = DateTime.Today;
    }

    private async void OnSearchLoanClick(object sender, RoutedEventArgs e)
    {
        var q = TxtSearchQuery.Text.Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        _loadedLoan = await _loanService.FindLoanByNumberOrBarcodeAsync(q);
        if (_loadedLoan == null)
        {
            MessageBox.Show($"Loan '{q}' not found.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DisplayLoanInfo(_loadedLoan);
        RecalculateInterest();
    }

    private void DisplayLoanInfo(Loan loan)
    {
        TxtCustomerName.Text = loan.Customer?.FullName ?? "-";
        TxtCustomerMobile.Text = loan.Customer?.MobileNumber ?? "-";
        TxtLoanNo.Text = loan.LoanNumber;
        TxtSchemeInfo.Text = $"{loan.Scheme?.Name} ({loan.Scheme?.InterestRatePerMonth:F2}%/mo)";
        TxtPledgeDate.Text = $"Pledged: {loan.PledgeDate:dd-MMM-yyyy}";
        TxtLastPaid.Text = $"Last Paid: {loan.LastInterestPaidDate:dd-MMM-yyyy}";
        TxtPrincipalBalance.Text = $"₹ {loan.LoanAmount:N2}";
        TxtNetWeight.Text = $"Net Wt: {loan.NetWeight:F3} g";
    }

    private void OnReceiptDateChanged(object sender, SelectionChangedEventArgs e)
    {
        RecalculateInterest();
    }

    private void RecalculateInterest()
    {
        if (_loadedLoan == null) return;

        var receiptDate = DateOnly.FromDateTime(DpReceiptDate.SelectedDate ?? DateTime.Today);
        var scheme = _loadedLoan.Scheme;

        _calcResult = _interestCalculator.CalculateInterest(
            _loadedLoan.LoanAmount,
            scheme?.InterestRatePerMonth ?? 1.50m,
            scheme?.PenalInterestRatePerMonth ?? 0.50m,
            _loadedLoan.PledgeDate,
            receiptDate,
            _loadedLoan.LastInterestPaidDate,
            scheme?.MaxTenureMonths ?? 12,
            scheme?.GraceDaysAfterExpiry ?? 7,
            scheme?.MinimumInterestDays ?? 30,
            roundUpToNextMonth: false
        );

        GridInterest.ItemsSource = _calcResult.Breakdown;
        TxtInterestAmount.Text = _calcResult.TotalInterest.ToString("F2");

        ApplyReceiptTypePreset();
        UpdateTotalReceivable();
    }

    private void OnReceiptTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyReceiptTypePreset();
        UpdateTotalReceivable();
    }

    private void ApplyReceiptTypePreset()
    {
        if (_loadedLoan == null || _calcResult == null) return;

        var typeIdx = CmbReceiptType.SelectedIndex;
        if (typeIdx == 0) // Interest Only
        {
            TxtPartPrincipal.Text = "0.00";
            TxtPartPrincipal.IsReadOnly = true;
        }
        else if (typeIdx == 1) // Part Payment
        {
            TxtPartPrincipal.IsReadOnly = false;
        }
        else if (typeIdx == 2) // Full Closure
        {
            TxtPartPrincipal.Text = _loadedLoan.LoanAmount.ToString("F2");
            TxtPartPrincipal.IsReadOnly = true;
        }
    }

    private void OnPaymentAmountChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTotalReceivable();
    }

    private void UpdateTotalReceivable()
    {
        if (TxtTotalReceivable == null || TxtInterestAmount == null || TxtPartPrincipal == null || TxtPostalCharges == null || TxtAdjustment == null)
            return;

        decimal.TryParse(TxtInterestAmount.Text, out var interest);
        decimal.TryParse(TxtPartPrincipal.Text, out var principal);
        decimal.TryParse(TxtPostalCharges.Text, out var postal);
        decimal.TryParse(TxtAdjustment.Text, out var adj);

        var total = interest + principal + postal - adj;
        TxtTotalReceivable.Text = $"₹ {Math.Max(0m, total):N2}";
    }

    private async void OnProcessReceiptClick(object sender, RoutedEventArgs e)
    {
        if (_loadedLoan == null || _calcResult == null)
        {
            MessageBox.Show("Load an active loan first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtInterestAmount.Text, out var interest);
        decimal.TryParse(TxtPartPrincipal.Text, out var principal);
        decimal.TryParse(TxtPostalCharges.Text, out var postal);
        decimal.TryParse(TxtAdjustment.Text, out var adj);

        var total = interest + principal + postal - adj;
        if (total <= 0)
        {
            MessageBox.Show("Total received amount must be greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
            Guid.Empty,
            UserRole.Owner
        );

        try
        {
            var receipt = await _receiptService.ProcessReceiptAsync(dto);

            if (rType == ReceiptType.Closure)
            {
                MessageBox.Show($"Loan {_loadedLoan.LoanNumber} is now FULLY CLOSED!\nReceipt #{receipt.ReceiptNumber} created.\nReady to inspect ornaments and print Release Slip (மீட்பு சீட்டு).",
                    "Loan Closed & Released", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Receipt #{receipt.ReceiptNumber} posted successfully!\nTotal collected: ₹{receipt.TotalAmount:N2}",
                    "Payment Processed", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Reload loan status
            _loadedLoan = await _loanService.GetLoanByIdAsync(_loadedLoan.Id);
            if (_loadedLoan != null) DisplayLoanInfo(_loadedLoan);
            RecalculateInterest();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Receipt processing failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

