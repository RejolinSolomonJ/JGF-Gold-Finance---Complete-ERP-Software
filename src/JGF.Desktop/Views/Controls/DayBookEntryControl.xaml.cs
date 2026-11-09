using System.Windows;
using System.Windows.Controls;
using JGF.Application.Accounting;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Serilog;

namespace JGF.Desktop.Views.Controls;

public partial class DayBookEntryControl : UserControl
{
    private readonly IAccountingReportService _accountingReport;
    private readonly IAccountingService _accountingService;
    private readonly User? _currentUser;

    public DayBookEntryControl(
        IAccountingReportService accountingReport,
        IAccountingService accountingService,
        User? currentUser = null)
    {
        InitializeComponent();
        _accountingReport = accountingReport;
        _accountingService = accountingService;
        _currentUser = currentUser;

        DpFrom.SelectedDate = DateTime.Today;
        DpTo.SelectedDate = DateTime.Today;

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var heads = await _accountingService.GetAllLedgerHeadsAsync();
            CmbLedgerHead.ItemsSource = heads;
            CmbLedgerHead.DisplayMemberPath = "Name";
            if (heads.Count > 0) CmbLedgerHead.SelectedIndex = 0;

            await LoadDayBookAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Day Book screen.");
            MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnRefreshDayBookClick(object sender, RoutedEventArgs e)
    {
        await LoadDayBookAsync();
    }

    private async Task LoadDayBookAsync()
    {
        var from = DateOnly.FromDateTime(DpFrom.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(DpTo.SelectedDate ?? DateTime.Today);

        try
        {
            var dayBook = await _accountingReport.GetDayBookAsync(from, to);

            TxtOpeningCash.Text = ValidationHelper.FormatCurrency(dayBook.OpeningCash);
            TxtTotalReceipts.Text = ValidationHelper.FormatCurrency(dayBook.TotalReceipts);
            TxtTotalPayments.Text = ValidationHelper.FormatCurrency(dayBook.TotalPayments);
            TxtClosingCash.Text = ValidationHelper.FormatCurrency(dayBook.ClosingCash);

            GridDayBook.ItemsSource = dayBook.Items;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load Day Book entries.");
            MessageBox.Show($"Failed to load day book: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnPostManualVoucherClick(object sender, RoutedEventArgs e)
    {
        if (CmbLedgerHead.SelectedItem is not LedgerHead head)
        {
            MessageBox.Show("Please select a ledger account.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("Please enter a valid amount greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vType = CmbVoucherType.SelectedIndex == 0 ? VoucherType.Receipt : VoucherType.Payment;
        var userId = _currentUser?.Id ?? Guid.Empty;
        var userRole = _currentUser?.Role ?? UserRole.Owner;

        var dto = new CreateManualVoucherDto(
            DateOnly.FromDateTime(DateTime.Today),
            vType,
            head.Id,
            amount,
            TxtNarration.Text.Trim(),
            userId,
            userRole
        );

        try
        {
            var voucherNo = await _accountingReport.PostManualVoucherAsync(dto);
            MessageBox.Show($"Manual voucher #{voucherNo} posted successfully!\nAmount: {ValidationHelper.FormatCurrency(amount)}", "Voucher Posted", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtAmount.Text = "0.00";
            await LoadDayBookAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to post manual voucher.");
            MessageBox.Show($"Posting failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
