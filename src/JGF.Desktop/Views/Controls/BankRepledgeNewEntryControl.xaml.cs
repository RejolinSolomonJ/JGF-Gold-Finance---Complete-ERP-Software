using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Repledge;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Serilog;

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
    private readonly User? _currentUser;

    public ObservableCollection<LoanRepledgeSelectionViewModel> AvailableLoans { get; } = new();

    public BankRepledgeNewEntryControl(
        IBankRepledgeService repledgeService,
        IBankService bankService,
        User? currentUser = null)
    {
        InitializeComponent();
        _repledgeService = repledgeService;
        _bankService = bankService;
        _currentUser = currentUser;

        GridAvailableLoans.ItemsSource = AvailableLoans;
        DpPledgeDate.SelectedDate = DateTime.Today;
        DpMaturityDate.SelectedDate = DateTime.Today.AddMonths(12);

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var banks = await _bankService.GetAllBanksAsync(activeOnly: true);
            CmbBank.ItemsSource = banks;
            CmbBank.DisplayMemberPath = "Name";
            if (banks.Count > 0) CmbBank.SelectedIndex = 0;

            await LoadAvailableLoansAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Bank Repledge screen.");
            MessageBox.Show($"Failed to load banks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadAvailableLoansAsync()
    {
        try
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
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load available loans for repledge.");
            MessageBox.Show($"Failed to load loans: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnBankChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbBank == null || TxtPledgeNumber == null || TxtInterestRate == null) return;
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
        if (TxtSelectedCount == null || TxtSelectedPrincipal == null || TxtSelectedWeight == null || TxtBankWeight == null || TxtBankDisbursedAmount == null)
            return;

        var selected = AvailableLoans.Where(l => l.IsSelected).ToList();
        var totalPrincipal = selected.Sum(l => l.LoanAmount);
        var totalWeight = selected.Sum(l => l.NetWeight);

        TxtSelectedCount.Text = $"{selected.Count} loans selected";
        TxtSelectedPrincipal.Text = ValidationHelper.FormatCurrency(totalPrincipal);
        TxtSelectedWeight.Text = ValidationHelper.FormatWeight(totalWeight);
        TxtBankWeight.Text = totalWeight.ToString("F3");

        var estimatedBankLtv = 0.70m;
        var estBankDisbursed = Math.Round(totalPrincipal * estimatedBankLtv, 2);
        TxtBankDisbursedAmount.Text = estBankDisbursed.ToString("F2");
    }

    private async void OnSaveBankPledgeClick(object sender, RoutedEventArgs e)
    {
        if (CmbBank.SelectedItem is not Bank bank)
        {
            MessageBox.Show("Please select a Bank or NBFC.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selected = AvailableLoans.Where(l => l.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Please select at least one customer loan to re-pledge.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtBankDisbursedAmount.Text, out var bankDisbursed) || bankDisbursed <= 0)
        {
            MessageBox.Show("Bank Disbursed Amount must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtInterestRate.Text, out var interestRate) || interestRate < 0)
        {
            MessageBox.Show("Bank Interest Rate cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtBankWeight.Text, out var bankWeight) || bankWeight < 0)
        {
            MessageBox.Show("Bank Appraised Weight cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pledgeDate = DateOnly.FromDateTime(DpPledgeDate.SelectedDate ?? DateTime.Today);
        var maturityDate = DateOnly.FromDateTime(DpMaturityDate.SelectedDate ?? DateTime.Today.AddMonths(12));

        if (maturityDate <= pledgeDate)
        {
            MessageBox.Show("Maturity Date must be later than the Pledging Date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var userId = _currentUser?.Id ?? Guid.Empty;
        var userRole = _currentUser?.Role ?? UserRole.Owner;

        var dto = new CreateBankPledgeDto(
            bank.Id,
            TxtPledgeNumber.Text.Trim(),
            pledgeDate,
            maturityDate,
            bankDisbursed,
            interestRate,
            bankWeight,
            TxtBankAccountNo.Text.Trim(),
            TxtAccountHolder.Text.Trim(),
            null,
            selected.Select(l => l.LoanId).ToList(),
            $"Pledged {selected.Count} customer loans to {bank.Name}",
            userId,
            userRole
        );

        try
        {
            var pledge = await _repledgeService.CreateBankPledgeAsync(dto);
            MessageBox.Show(
                $"Bank Repledge #{pledge.PledgeNumber} created successfully!\n" +
                $"Cash Inflow: {ValidationHelper.FormatCurrency(pledge.DisbursedAmount)}\n" +
                $"Customer jewels marked 'In Bank Custody ({bank.Name})'.\n" +
                $"Voucher posted: Dr Cash, Cr Bank Loan Payable",
                "Repledge Created Successfully",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await LoadAvailableLoansAsync();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
            Log.Error(ex, "Failed to create bank pledge.");
            MessageBox.Show($"Failed to save bank pledge: {msg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
