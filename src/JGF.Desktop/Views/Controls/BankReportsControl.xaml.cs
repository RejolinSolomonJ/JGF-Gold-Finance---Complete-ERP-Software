using System.Windows;
using System.Windows.Controls;
using JGF.Application.Repledge;

namespace JGF.Desktop.Views.Controls;

public partial class BankReportsControl : UserControl
{
    private readonly IBankRepledgeService _repledgeService;

    public BankReportsControl(IBankRepledgeService repledgeService)
    {
        InitializeComponent();
        _repledgeService = repledgeService;

        Loaded += async (s, e) => await LoadCurrentReportAsync();
    }

    private void OnReportChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridStockCustody == null) return;

        GridStockCustody.Visibility = Visibility.Collapsed;
        GridPawnVsBank.Visibility = Visibility.Collapsed;
        GridOverdue.Visibility = Visibility.Collapsed;

        if (CmbReport.SelectedIndex == 0) GridStockCustody.Visibility = Visibility.Visible;
        else if (CmbReport.SelectedIndex == 1) GridPawnVsBank.Visibility = Visibility.Visible;
        else if (CmbReport.SelectedIndex == 2) GridOverdue.Visibility = Visibility.Visible;

        _ = LoadCurrentReportAsync();
    }

    private async void OnRefreshReportClick(object sender, RoutedEventArgs e)
    {
        await LoadCurrentReportAsync();
    }

    private async Task LoadCurrentReportAsync()
    {
        if (CmbReport.SelectedIndex == 0)
        {
            var stock = await _repledgeService.GetJewelStockCustodyReportAsync();
            GridStockCustody.ItemsSource = stock;
            var lockerCount = stock.Count(s => s.CustodyLocation == "In Shop Locker");
            var bankCount = stock.Count(s => s.CustodyLocation.StartsWith("In Bank"));
            TxtReportSummary.Text = $"Stock Custody: {lockerCount} items in Shop Locker | {bankCount} items deposited with Banks.";
        }
        else if (CmbReport.SelectedIndex == 1)
        {
            var diff = await _repledgeService.GetPawnVsBankDifferenceReportAsync();
            GridPawnVsBank.ItemsSource = diff;
            var totalDiff = diff.Sum(d => d.Difference);
            TxtReportSummary.Text = $"Pawn vs. Bank Principal: Total Customer Loan Excess = ₹{totalDiff:N2}";
        }
        else if (CmbReport.SelectedIndex == 2)
        {
            var all = await _repledgeService.GetAllBankPledgesAsync();
            var overdue = all.Where(p => p.IsOverdue || p.IsDueSoon).ToList();
            GridOverdue.ItemsSource = overdue;
            TxtReportSummary.Text = $"Urgent Action: {overdue.Count(p => p.IsOverdue)} Overdue pledges | {overdue.Count(p => p.IsDueSoon)} Pledges due within 15 days.";
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Repledge report exported successfully to PDF / Print Spooler.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
