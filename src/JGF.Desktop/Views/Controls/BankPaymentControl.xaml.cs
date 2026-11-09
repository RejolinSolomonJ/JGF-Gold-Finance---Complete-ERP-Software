using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JGF.Application.Repledge;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Serilog;

namespace JGF.Desktop.Views.Controls;

public class LinkedLoanReleaseViewModel : INotifyPropertyChanged
{
    private bool _isSelectedForRelease;
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public decimal LoanAmount { get; set; }
    public string CustodyStatus { get; set; } = string.Empty;
    public bool IsAlreadyReleased { get; set; }

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
    private readonly User? _currentUser;
    private BankPledge? _loadedPledge;

    public ObservableCollection<LinkedLoanReleaseViewModel> LinkedLoans { get; } = new();

    public BankPaymentControl(IBankRepledgeService repledgeService, User? currentUser = null)
    {
        InitializeComponent();
        _repledgeService = repledgeService;
        _currentUser = currentUser;

        GridLinkedLoans.ItemsSource = LinkedLoans;
        DpPaymentDate.SelectedDate = DateTime.Today;
    }

    public async Task LoadPledgeByNumberDirectAsync(string pledgeNumber)
    {
        TxtSearchQuery.Text = pledgeNumber;
        await LoadPledgeAsync(pledgeNumber);
    }

    private async void OnLoadPledgeClick(object sender, RoutedEventArgs e)
    {
        await LoadPledgeAsync(TxtSearchQuery.Text.Trim());
    }

    private async void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await LoadPledgeAsync(TxtSearchQuery.Text.Trim());
        }
    }

    private async Task LoadPledgeAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            MessageBox.Show("Please enter a Bank Pledge Number or Account Number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var allPledges = await _repledgeService.GetAllBankPledgesAsync();
            var matched = allPledges.FirstOrDefault(p =>
                p.PledgeNumber.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(p.BankAccountOrLoanNumber) && p.BankAccountOrLoanNumber.Equals(query, StringComparison.OrdinalIgnoreCase)));

            if (matched == null)
            {
                MessageBox.Show($"Bank Pledge not found: '{query}'", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var pledge = await _repledgeService.GetBankPledgeDetailsAsync(matched.Id);
            if (pledge == null)
            {
                MessageBox.Show($"Bank Pledge details not found for '{query}'", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _loadedPledge = pledge;
            DisplayPledgeInfo(pledge);
            await LoadLinkedLoansAsync(pledge.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load bank pledge.");
            MessageBox.Show($"Failed to load pledge: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DisplayPledgeInfo(BankPledge p)
    {
        TxtBankName.Text = p.Bank?.Name ?? "Bank";
        TxtBankAccount.Text = $"A/C: {p.BankAccountOrLoanNumber ?? "-"}";
        TxtPledgeNo.Text = p.PledgeNumber;
        TxtRateAndMaturity.Text = $"Rate: {p.InterestRate:F2}%/mo | Due: {ValidationHelper.FormatDate(p.MaturityDate)}";

        var monthlyInterest = Math.Round(p.DisbursedAmount * (p.InterestRate / 100m), 2);
        TxtEstMonthlyInterest.Text = $"{ValidationHelper.FormatCurrency(monthlyInterest)} / mo";
        TxtActiveBankBalance.Text = ValidationHelper.FormatCurrency(p.DisbursedAmount);
        TxtStatus.Text = $"Status: {p.Status}";

        TxtInterestPaid.Text = monthlyInterest.ToString("F2");
        TxtPrincipalPaid.Text = "0.00";
        TxtPostalCharges.Text = "0.00";
        UpdateTotalOutflow();
    }

    private async Task LoadLinkedLoansAsync(Guid pledgeId)
    {
        LinkedLoans.Clear();
        var details = await _repledgeService.GetBankPledgeDetailsAsync(pledgeId);
        if (details?.BankPledgeLoans != null)
        {
            foreach (var pl in details.BankPledgeLoans)
            {
                var isReleased = pl.IsReleased || pl.ReleasedDate.HasValue;
                LinkedLoans.Add(new LinkedLoanReleaseViewModel
                {
                    LoanId = pl.LoanId,
                    LoanNumber = pl.Loan?.LoanNumber ?? "-",
                    CustomerName = pl.Loan?.Customer?.FullName ?? "-",
                    NetWeight = pl.Loan?.NetWeight ?? 0m,
                    LoanAmount = pl.PledgedAmount,
                    CustodyStatus = isReleased ? $"Released on {ValidationHelper.FormatDate(pl.ReleasedDate)}" : "In Bank Custody",
                    IsAlreadyReleased = isReleased,
                    IsSelectedForRelease = false
                });
            }
        }
    }

    private void OnPaymentTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadedPledge == null) return;

        if (CmbPaymentType.SelectedIndex == 0) // Interest Only
        {
            TxtPrincipalPaid.Text = "0.00";
            foreach (var l in LinkedLoans) l.IsSelectedForRelease = false;
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

        interest = Math.Max(0m, interest);
        principal = Math.Max(0m, principal);
        postal = Math.Max(0m, postal);

        var total = interest + principal + postal;
        TxtTotalOutflow.Text = ValidationHelper.FormatCurrency(total);
    }

    private async void OnSaveBankPaymentClick(object sender, RoutedEventArgs e)
    {
        if (_loadedPledge == null)
        {
            MessageBox.Show("Please load an active bank pledge first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtInterestPaid.Text, out var interest) || interest < 0)
        {
            MessageBox.Show("Interest paid cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtPrincipalPaid.Text, out var principal) || principal < 0)
        {
            MessageBox.Show("Principal repaid cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtPostalCharges.Text, out var postal) || postal < 0)
        {
            MessageBox.Show("Bank charges cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var total = interest + principal + postal;
        if (total <= 0)
        {
            MessageBox.Show("Total bank payment amount must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        var userId = _currentUser?.Id ?? Guid.Empty;
        var userRole = _currentUser?.Role ?? UserRole.Owner;

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
            userId,
            userRole
        );

        try
        {
            BtnSavePayment.IsEnabled = false;
            var (payment, rzReceiptNumber) = await _repledgeService.ProcessBankPaymentAsync(dto);

            var releaseMsg = releasedIds.Count > 0 ? $"\n{releasedIds.Count} customer jewel packet(s) released back to Shop Locker." : "";
            MessageBox.Show(
                $"Bank Payment #{rzReceiptNumber} posted successfully!\n" +
                $"Total Outflow: {ValidationHelper.FormatCurrency(payment.TotalAmount)}{releaseMsg}\n" +
                $"Voucher posted: Dr Bank Loan / Charges, Cr Cash",
                "Payment Posted",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await LoadPledgeAsync(_loadedPledge.PledgeNumber);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to post bank payment.");
            MessageBox.Show($"Bank payment failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSavePayment.IsEnabled = true;
        }
    }
}
