using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Repledge;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public class LoanRepledgeSelectionViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateOnly PledgeDate { get; set; }
    public decimal NetWeight { get; set; }
    public decimal LoanAmount { get; set; }
    public string PacketNumber { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class BankRepledgeNewEntryControl : UserControl
{
    private readonly IBankRepledgeService _repledgeService;
    private readonly IBankService _bankService;

    public ObservableCollection<LoanRepledgeSelectionViewModel> AvailableLoans { get; } = new();

    public BankRepledgeNewEntryControl(
        IBankRepledgeService repledgeService,
        IBankService bankService)
    {
        InitializeComponent();
        _repledgeService = repledgeService;
        _bankService = bankService;

        GridAvailableLoans.ItemsSource = AvailableLoans;
        DpPledgeDate.SelectedDate = DateTime.Today;
        DpMaturityDate.SelectedDate = DateTime.Today.AddMonths(12);

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var banks = await _bankService.GetAllBanksAsync(activeOnly: true);
        CmbBank.ItemsSource = banks;
        CmbBank.DisplayMemberPath = "Name";
        if (banks.Count > 0) CmbBank.SelectedIndex = 0;

        await LoadAvailableLoansAsync();
    }

    private async Task LoadAvailableLoansAsync()
    {
        AvailableLoans.Clear();
        var loans = await _repledgeService.GetAvailableLoansForRepledgeAsync();

        foreach (var l in loans)
        {
            var vm = new LoanRepledgeSelectionViewModel
            {
                LoanId = l.Id,
                LoanNumber = l.LoanNumber,
                CustomerName = l.Customer?.FullName ?? "-",
                PledgeDate = l.PledgeDate,
                NetWeight = l.NetWeight,
                LoanAmount = l.LoanAmount,
                PacketNumber = l.PacketNumber ?? "-"
            };
            vm.PropertyChanged += (s, e) => RecalculateSelectedSummary();
            AvailableLoans.Add(vm);
        }

        RecalculateSelectedSummary();
    }

    private void OnBankChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbBank.SelectedItem is Bank b)
        {
            TxtPledgeNumber.Text = $"BP-{b.Code}-{DateTime.UtcNow:yyMMdd}";
            TxtInterestRate.Text = b.InterestRate > 0 ? b.InterestRate.ToString("F2") : "0.75";
        }
    }

    private void OnSelectAllLoansClick(object sender, RoutedEventArgs e)
    {
        var anyUnselected = AvailableLoans.Any(l => !l.IsSelected);
        foreach (var l in AvailableLoans) l.IsSelected = anyUnselected;
        RecalculateSelectedSummary();
    }

    private void RecalculateSelectedSummary()
    {
        var selected = AvailableLoans.Where(l => l.IsSelected).ToList();
        var totalPrincipal = selected.Sum(l => l.LoanAmount);
        var totalWeight = selected.Sum(l => l.NetWeight);

        TxtSelectedCount.Text = $"{selected.Count} loans selected";
        TxtSelectedPrincipal.Text = $"₹ {totalPrincipal:N2}";
        TxtSelectedWeight.Text = $"{totalWeight:F3} g";
        TxtBankWeight.Text = totalWeight.ToString("F3");

        // By default, assume bank lends ~70-75% of customer principal
        var estimatedBankLtv = 0.70m;
        var estBankDisbursed = Math.Round(totalPrincipal * estimatedBankLtv, 2);
        TxtBankDisbursedAmount.Text = estBankDisbursed.ToString("F2");
    }

    private async void OnSaveBankPledgeClick(object sender, RoutedEventArgs e)
    {
        if (CmbBank.SelectedItem is not Bank bank)
        {
            MessageBox.Show("Select a Bank/NBFC.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selected = AvailableLoans.Where(l => l.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one customer loan to pledge.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtBankDisbursedAmount.Text, out var bankDisbursed);
        decimal.TryParse(TxtInterestRate.Text, out var interestRate);
        decimal.TryParse(TxtBankWeight.Text, out var bankWeight);

        if (bankDisbursed <= 0)
        {
            MessageBox.Show("Bank Disbursed Amount must be greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dto = new CreateBankPledgeDto(
            bank.Id,
            TxtPledgeNumber.Text.Trim(),
            DateOnly.FromDateTime(DpPledgeDate.SelectedDate ?? DateTime.Today),
            DateOnly.FromDateTime(DpMaturityDate.SelectedDate ?? DateTime.Today.AddMonths(12)),
            bankDisbursed,
            interestRate,
            bankWeight,
            TxtBankAccountNo.Text.Trim(),
            TxtAccountHolder.Text.Trim(),
            null,
            selected.Select(l => l.LoanId).ToList(),
            $"Pledged {selected.Count} customer loans to {bank.Name}",
            Guid.Empty,
            UserRole.Owner
        );

        try
        {
            var pledge = await _repledgeService.CreateBankPledgeAsync(dto);
            MessageBox.Show(
                $"Bank Repledge #{pledge.PledgeNumber} created successfully!\n" +
                $"Cash Inflow: ₹{pledge.DisbursedAmount:N2}\n" +
                $"Collateral jewels are now marked 'In Bank Custody ({bank.Name})'.\n" +
                $"Voucher posted: Dr Cash ₹{pledge.DisbursedAmount:N2}, Cr Bank Loan Payable ₹{pledge.DisbursedAmount:N2}",
                "Repledge Disbursed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await LoadAvailableLoansAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save bank pledge: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
