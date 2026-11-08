using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Repledge;
using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public class LinkedLoanReleaseViewModel : INotifyPropertyChanged
{
    private bool _isSelectedForRelease;

    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public decimal LoanAmount { get; set; }
    public bool IsAlreadyReleased { get; set; }
    public string CustodyStatus => IsAlreadyReleased ? "✓ Released to Shop Locker" : "⚠ In Bank Custody";

    public bool IsSelectedForRelease
    {
        get => _isSelectedForRelease;
        set
        {
            if (_isSelectedForRelease != value)
            {
                _isSelectedForRelease = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedForRelease)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class BankPaymentControl : UserControl
{
    private readonly IBankRepledgeService _repledgeService;
    private BankPledge? _loadedPledge;

    public ObservableCollection<LinkedLoanReleaseViewModel> LinkedLoans { get; } = new();

    public BankPaymentControl(IBankRepledgeService repledgeService)
    {
        InitializeComponent();
        _repledgeService = repledgeService;

        GridLinkedLoans.ItemsSource = LinkedLoans;
        DpPaymentDate.SelectedDate = DateTime.Today;
    }

    private async void OnLoadPledgeClick(object sender, RoutedEventArgs e)
    {
        var q = TxtSearchQuery.Text.Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        var allPledges = await _repledgeService.GetAllBankPledgesAsync();
        var match = allPledges.FirstOrDefault(p => 
            p.PledgeNumber.Equals(q, StringComparison.OrdinalIgnoreCase) || 
            (p.BankAccountOrLoanNumber != null && p.BankAccountOrLoanNumber.Equals(q, StringComparison.OrdinalIgnoreCase)));

        if (match == null)
        {
            MessageBox.Show($"Bank pledge '{q}' not found.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _loadedPledge = await _repledgeService.GetBankPledgeDetailsAsync(match.Id);
        if (_loadedPledge != null) DisplayPledgeInfo(_loadedPledge);
    }

    private void DisplayPledgeInfo(BankPledge p)
    {
        TxtBankName.Text = p.Bank?.Name ?? "-";
        TxtBankAccount.Text = $"A/C: {p.BankAccountOrLoanNumber ?? "N/A"}";
        TxtPledgeNo.Text = p.PledgeNumber;
        TxtRateAndMaturity.Text = $"Rate: {p.InterestRate:F2}%/mo | Due: {p.MaturityDate:dd-MMM-yyyy}";

        var monthlyInterest = Math.Round(p.DisbursedAmount * (p.InterestRate / 100m), 2);
        TxtEstMonthlyInterest.Text = $"₹ {monthlyInterest:N2} / mo";
        TxtActiveBankBalance.Text = $"₹ {p.DisbursedAmount:N2}";
        TxtStatus.Text = $"Status: {p.Status}";

        LinkedLoans.Clear();
        foreach (var bpl in p.BankPledgeLoans)
        {
            LinkedLoans.Add(new LinkedLoanReleaseViewModel
            {
                LoanId = bpl.LoanId,
                LoanNumber = bpl.Loan?.LoanNumber ?? "-",
                CustomerName = bpl.Loan?.Customer?.FullName ?? "-",
                NetWeight = bpl.Loan?.NetWeight ?? 0m,
                LoanAmount = bpl.Loan?.LoanAmount ?? 0m,
                IsAlreadyReleased = bpl.IsReleased,
                IsSelectedForRelease = false
            });
        }

        TxtInterestPaid.Text = monthlyInterest.ToString("F2");
        UpdateTotalOutflow();
    }

    private void OnPaymentTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadedPledge == null) return;

        if (CmbPaymentType.SelectedIndex == 0) // Monthly Interest Only
        {
            TxtPrincipalPaid.Text = "0.00";
            foreach (var l in LinkedLoans) l.IsSelectedForRelease = false;
        }
        else if (CmbPaymentType.SelectedIndex == 1) // Part Principal
        {
            // Allow checking specific loans
        }
        else if (CmbPaymentType.SelectedIndex == 2) // Full Closure
        {
            TxtPrincipalPaid.Text = _loadedPledge.DisbursedAmount.ToString("F2");
            foreach (var l in LinkedLoans)
            {
                if (!l.IsAlreadyReleased) l.IsSelectedForRelease = true;
            }
        }
        UpdateTotalOutflow();
    }

    private void OnPaymentAmountChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTotalOutflow();
    }

    private void UpdateTotalOutflow()
    {
        if (TxtTotalOutflow == null || TxtInterestPaid == null || TxtPrincipalPaid == null || TxtPostalCharges == null)
            return;

        decimal.TryParse(TxtInterestPaid.Text, out var interest);
        decimal.TryParse(TxtPrincipalPaid.Text, out var principal);
        decimal.TryParse(TxtPostalCharges.Text, out var postal);

        var total = interest + principal + postal;
        TxtTotalOutflow.Text = $"₹ {total:N2}";
    }

    private async void OnSaveBankPaymentClick(object sender, RoutedEventArgs e)
    {
        if (_loadedPledge == null)
        {
            MessageBox.Show("Load an active bank pledge first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtInterestPaid.Text, out var interest);
        decimal.TryParse(TxtPrincipalPaid.Text, out var principal);
        decimal.TryParse(TxtPostalCharges.Text, out var postal);

        var total = interest + principal + postal;
        if (total <= 0)
        {
            MessageBox.Show("Total payment amount must be greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var payType = CmbPaymentType.SelectedIndex switch
        {
            0 => BankPaymentType.InterestPayment,
            1 => BankPaymentType.PartRelease,
            2 => BankPaymentType.FullRelease,
            _ => BankPaymentType.InterestPayment
        };

        var releasedIds = LinkedLoans
            .Where(l => l.IsSelectedForRelease && !l.IsAlreadyReleased)
            .Select(l => l.LoanId)
            .ToList();

        var dto = new SettleBankPaymentDto(
            _loadedPledge.Id,
            DateOnly.FromDateTime(DpPaymentDate.SelectedDate ?? DateTime.Today),
            payType,
            principal,
            interest,
            postal,
            releasedIds,
            null,
            $"Bank payment to {_loadedPledge.Bank?.Name}",
            Guid.Empty,
            UserRole.Owner
        );

        try
        {
            var result = await _repledgeService.ProcessBankPaymentAsync(dto);
            MessageBox.Show(
                $"Bank Payment Voucher posted successfully under Series #{result.RzReceiptNumber}!\n" +
                $"Total Paid: ₹{total:N2}\n" +
                $"Released Customer Loans: {releasedIds.Count} (Returned to Shop Locker)",
                "Payment Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Reload pledge
            _loadedPledge = await _repledgeService.GetBankPledgeDetailsAsync(_loadedPledge.Id);
            if (_loadedPledge != null) DisplayPledgeInfo(_loadedPledge);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Payment failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
