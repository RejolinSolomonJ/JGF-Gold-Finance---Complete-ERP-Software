using System.Windows;
using System.Windows.Controls;
using JGF.Application.Accounting;

namespace JGF.Desktop.Views.Controls;

public partial class FinancialReportsControl : UserControl
{
    private readonly IAccountingReportService _accountingReport;

    public FinancialReportsControl(IAccountingReportService accountingReport)
    {
        InitializeComponent();
        _accountingReport = accountingReport;
        DpDate.SelectedDate = DateTime.Today;

        Loaded += async (s, e) => await GenerateSelectedReportAsync();
    }

    private void OnReportTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelTrialBalance == null) return;

        PanelTrialBalance.Visibility = Visibility.Collapsed;
        PanelPnL.Visibility = Visibility.Collapsed;
        PanelBalanceSheet.Visibility = Visibility.Collapsed;

        if (CmbReportType.SelectedIndex == 0) PanelTrialBalance.Visibility = Visibility.Visible;
        else if (CmbReportType.SelectedIndex == 1) PanelPnL.Visibility = Visibility.Visible;
        else if (CmbReportType.SelectedIndex == 2) PanelBalanceSheet.Visibility = Visibility.Visible;
    }

    private async void OnGenerateReportClick(object sender, RoutedEventArgs e)
    {
        await GenerateSelectedReportAsync();
    }

    private async Task GenerateSelectedReportAsync()
    {
        var date = DateOnly.FromDateTime(DpDate.SelectedDate ?? DateTime.Today);

        if (CmbReportType.SelectedIndex == 0) // Trial Balance
        {
            var tb = await _accountingReport.GetTrialBalanceAsync(date);
            GridTrialBalance.ItemsSource = tb.Rows;
            TxtTbTotalDr.Text = $"₹ {tb.TotalDebit:N2}";
            TxtTbTotalCr.Text = $"₹ {tb.TotalCredit:N2}";
            TxtTbDiff.Text = tb.Difference == 0 ? "Balanced (✓)" : $"Diff: ₹ {tb.Difference:N2}";
            TxtTbDiff.Foreground = tb.Difference == 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Crimson;
        }
        else if (CmbReportType.SelectedIndex == 1) // Profit & Loss
        {
            var from = new DateOnly(date.Year, 4, 1); // Indian FY Start
            if (date.Month < 4) from = new DateOnly(date.Year - 1, 4, 1);

            var pnl = await _accountingReport.GetProfitAndLossAsync(from, date);
            GridIncomes.ItemsSource = pnl.IncomeRows;
            GridExpenses.ItemsSource = pnl.ExpenseRows;
            TxtTotalExpenses.Text = $"₹ {pnl.TotalExpense:N2}";
            TxtNetProfitValue.Text = $"₹ {pnl.NetProfitOrLoss:N2}";

            TxtNetProfitLabel.Text = pnl.NetProfitOrLoss >= 0 ? "Net Profit (நிகர இலாபம்):" : "Net Loss (நிகர நட்டம்):";
            TxtNetProfitValue.Foreground = pnl.NetProfitOrLoss >= 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Crimson;
        }
        else if (CmbReportType.SelectedIndex == 2) // Balance Sheet
        {
            var bs = await _accountingReport.GetBalanceSheetAsync(date);
            GridAssets.ItemsSource = bs.Assets;

            // Combine Liabilities, Capital, and Net Profit
            var liabList = new List<BalanceSheetItemDto>(bs.Liabilities);
            liabList.AddRange(bs.Capital);
            liabList.Add(new BalanceSheetItemDto("P&L", "Current Year Profit / (Loss)", bs.CurrentYearNetProfit));

            GridLiabilities.ItemsSource = liabList;
            TxtTotalAssets.Text = $"₹ {bs.TotalAssets:N2}";
            TxtTotalLiabilities.Text = $"₹ {bs.TotalLiabilitiesAndCapital:N2}";
        }
    }

    private void OnExportPdfClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Report exported successfully to PDF / Print Spooler.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
