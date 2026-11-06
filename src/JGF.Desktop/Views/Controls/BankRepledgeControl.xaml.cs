using System.Windows;
using System.Windows.Controls;
using JGF.Application.Repledge;
using JGF.Application.Services;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public partial class BankRepledgeControl : UserControl
{
    private readonly IBankRepledgeService _repledgeService;
    private readonly IBankService _bankService;
    private BankPledgeSummaryDto? _selectedSummary;

    public BankRepledgeControl(
        IBankRepledgeService repledgeService,
        IBankService bankService)
    {
        InitializeComponent();
        _repledgeService = repledgeService;
        _bankService = bankService;

        Loaded += async (s, e) => await LoadPledgesAsync();
    }

    private async Task LoadPledgesAsync()
    {
        var list = await _repledgeService.GetAllBankPledgesAsync();
        GridPledges.ItemsSource = list;
    }

    private void OnPledgeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GridPledges.SelectedItem is BankPledgeSummaryDto s)
        {
            _selectedSummary = s;
            TxtSelectedInfo.Text = $"{s.BankName} — Pledge #{s.PledgeNumber} | Active Disbursed: ₹{s.DisbursedAmount:N2}";

            // Estimate monthly interest
            var estInterest = Math.Round(s.DisbursedAmount * (s.InterestRate / 100m), 2);
            TxtPaymentAmount.Text = estInterest.ToString("F2");
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadPledgesAsync();
    }

    private async void OnNewPledgeClick(object sender, RoutedEventArgs e)
    {
        var banks = await _bankService.GetAllBanksAsync(activeOnly: true);
        if (banks.Count == 0)
        {
            MessageBox.Show("Please configure a Bank/NBFC in Masters first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var availableLoans = await _repledgeService.GetAvailableLoansForRepledgeAsync();
        if (availableLoans.Count == 0)
        {
            MessageBox.Show("No active customer loans available to repledge.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Auto-bundle up to 5 customer loans for refinance demo
        var loansToPledge = availableLoans.Take(5).ToList();
        var totalCustomerLoan = loansToPledge.Sum(l => l.LoanAmount);
        var bankLtv = 0.70m;
        var bankBorrowAmount = Math.Round(totalCustomerLoan * bankLtv, 2);

        var bank = banks.First();
        var pledgeNumber = $"BP-{bank.Code}-{DateTime.UtcNow:yyMMddHHmm}";

        var dto = new CreateBankPledgeDto(
            bank.Id,
            pledgeNumber,
            DateOnly.FromDateTime(DateTime.Today),
            DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            bankBorrowAmount,
            bank.InterestRate > 0 ? bank.InterestRate : 0.75m,
            loansToPledge.Select(l => l.Id).ToList(),
            $"Repledged {loansToPledge.Count} loans to {bank.Name}",
            Guid.Empty,
            UserRole.Owner
        );

        try
        {
            var result = await _repledgeService.CreateBankPledgeAsync(dto);
            MessageBox.Show(
                $"Bank Repledge #{result.PledgeNumber} created successfully!\n" +
                $"Bank: {bank.Name}\n" +
                $"Customer Loans Pledged: {loansToPledge.Count} (Total: ₹{totalCustomerLoan:N2})\n" +
                $"Bank Disbursed to JGF: ₹{bankBorrowAmount:N2}\n\n" +
                $"Voucher Posted: Dr Cash ₹{bankBorrowAmount:N2}, Cr Bank Loan Payable (2001) ₹{bankBorrowAmount:N2}",
                "Repledge Created",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await LoadPledgesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Repledge creation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnPostBankPaymentClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSummary == null)
        {
            MessageBox.Show("Select a bank pledge first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtPaymentAmount.Text, out var amount);
        if (amount <= 0)
        {
            MessageBox.Show("Enter a payment amount greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var payType = CmbAction.SelectedIndex switch
        {
            0 => BankPaymentType.InterestPayment,
            1 => BankPaymentType.PartRelease,
            2 => BankPaymentType.FullRelease,
            _ => BankPaymentType.InterestPayment
        };

        var dto = new SettleBankRepledgeDto(
            _selectedSummary.Id,
            DateOnly.FromDateTime(DateTime.Today),
            payType,
            amount,
            null,
            null,
            $"Payment to {_selectedSummary.BankName}",
            Guid.Empty,
            UserRole.Owner
        );

        try
        {
            await _repledgeService.SettleBankPaymentAsync(dto);
            MessageBox.Show($"Bank payment of ₹{amount:N2} posted successfully!", "Payment Recorded", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadPledgesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Payment failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
