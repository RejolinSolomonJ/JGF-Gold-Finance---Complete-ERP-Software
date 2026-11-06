using System.Windows;
using System.Windows.Controls;
using JGF.Application.Accounting;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public partial class DayBookEntryControl : UserControl
{
    private readonly IAccountingReportService _accountingReport;
    private readonly IAccountingService _accountingMaster;

    public DayBookEntryControl(
        IAccountingReportService accountingReport,
        IAccountingService accountingMaster)
    {
        InitializeComponent();
        _accountingReport = accountingReport;
        _accountingMaster = accountingMaster;

        DpFrom.SelectedDate = DateTime.Today;
        DpTo.SelectedDate = DateTime.Today;

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Load Ledger Heads for manual dropdown
        var heads = await _accountingMaster.GetAllLedgerHeadsAsync();
        CmbLedgerHead.ItemsSource = heads.Where(h => h.Code != "1001").ToList(); // Exclude cash
        CmbLedgerHead.DisplayMemberPath = "Name";
        if (CmbLedgerHead.Items.Count > 0) CmbLedgerHead.SelectedIndex = 0;

        await LoadDayBookAsync();
    }

    private async Task LoadDayBookAsync()
    {
        var from = DateOnly.FromDateTime(DpFrom.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(DpTo.SelectedDate ?? DateTime.Today);

        var summary = await _accountingReport.GetDayBookAsync(from, to);

        TxtOpeningCash.Text = $"₹ {summary.OpeningCash:N2}";
        TxtTotalReceipts.Text = $"₹ {summary.TotalReceipts:N2}";
        TxtTotalPayments.Text = $"₹ {summary.TotalPayments:N2}";
        TxtClosingCash.Text = $"₹ {summary.ClosingCash:N2}";

        GridDayBook.ItemsSource = summary.Items;
    }

    private async void OnRefreshDayBookClick(object sender, RoutedEventArgs e)
    {
        await LoadDayBookAsync();
    }

    private async void OnPostManualVoucherClick(object sender, RoutedEventArgs e)
    {
        if (CmbLedgerHead.SelectedItem is not LedgerHead head)
        {
            MessageBox.Show("Select a ledger account.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtAmount.Text, out var amount);
        if (amount <= 0)
        {
            MessageBox.Show("Please enter an amount greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vType = CmbVoucherType.SelectedIndex == 0 ? VoucherType.Receipt : VoucherType.Payment;
        var date = DateOnly.FromDateTime(DpTo.SelectedDate ?? DateTime.Today);

        var dto = new CreateManualVoucherDto(
            date,
            vType,
            head.Id,
            amount,
            TxtNarration.Text.Trim(),
            Guid.Empty,
            UserRole.Owner
        );

        try
        {
            var voucherNo = await _accountingReport.PostManualVoucherAsync(dto);
            MessageBox.Show($"Manual voucher #{voucherNo} posted successfully!", "Voucher Posted", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtAmount.Text = "0.00";
            await LoadDayBookAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Posting failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
