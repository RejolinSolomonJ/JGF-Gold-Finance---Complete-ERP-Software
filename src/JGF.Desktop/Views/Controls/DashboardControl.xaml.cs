using System.Windows;
using System.Windows.Controls;
using JGF.Application.Dashboard;

namespace JGF.Desktop.Views.Controls;

public partial class DashboardControl : UserControl
{
    private readonly IDashboardService _dashboardService;
    private readonly Action<string>? _navigationCallback;

    public DashboardControl(IDashboardService dashboardService, Action<string>? navigationCallback = null)
    {
        InitializeComponent();
        _dashboardService = dashboardService;
        _navigationCallback = navigationCallback;

        Loaded += async (s, e) => await LoadDashboardDataAsync();
    }

    public async Task LoadDashboardDataAsync()
    {
        try
        {
            var data = await _dashboardService.GetDashboardSummaryAsync();

            TxtTodayCash.Text = $"₹ {data.TodayCashBalance:N2}";
            TxtLoansToday.Text = $"₹ {data.LoansIssuedTodayAmount:N2}";
            TxtLoansTodayCount.Text = $"{data.LoansIssuedTodayCount} loans disbursed today";

            TxtCollectionsToday.Text = $"₹ {data.CollectionsTodayTotal:N2}";
            TxtCollectionsTodayBreakdown.Text = $"Pri: ₹{data.CollectionsTodayPrincipal:N2} | Int: ₹{data.CollectionsTodayInterest:N2}";

            TxtTotalOutstanding.Text = $"₹ {data.TotalOutstandingPrincipal:N2}";
            TxtActiveLoansCount.Text = $"{data.TotalActiveLoansCount} active loan accounts";

            TxtTotalGoldStock.Text = $"{data.TotalGoldWeightOverall:F3} g";
            TxtGoldStockSplit.Text = $"Locker: {data.TotalGoldWeightShopLocker:F3} g | Bank: {data.TotalGoldWeightBankCustody:F3} g";

            TxtLoansMonth.Text = $"₹ {data.LoansIssuedThisMonthAmount:N2}";
            TxtLoansMonthCount.Text = $"{data.LoansIssuedThisMonthCount} loans issued this month";

            TxtOverdueLoans.Text = $"₹ {data.OverdueLoansPrincipal:N2}";
            TxtOverdueCount.Text = $"{data.OverdueLoansCount} accounts overdue";

            TxtBankExposure.Text = $"₹ {data.BankRepledgeTotalExposure:N2}";
            if (data.HasRecentSuccessfulBackup)
            {
                TxtBackupStatus.Text = $"Backup: OK ({data.LastBackupTimeUtc?.ToLocalTime():dd-MMM HH:mm})";
                TxtBackupStatus.Foreground = System.Windows.Media.Brushes.DarkGreen;
            }
            else
            {
                TxtBackupStatus.Text = "⚠ Backup: Overdue (>24h)";
                TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Red;
            }

            GridMonthlyTrends.ItemsSource = data.MonthlyTrends;
            GridUpcomingDues.ItemsSource = data.BankUpcomingDues;

            TxtLastRefreshed.Text = $"Updated: {DateTime.Now:hh:mm:ss tt}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load dashboard KPIs: {ex.Message}", "Dashboard Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadDashboardDataAsync();
    }

    private void OnNewLoanClick(object sender, RoutedEventArgs e) => _navigationCallback?.Invoke("NewLoan");
    private void OnReceiptClick(object sender, RoutedEventArgs e) => _navigationCallback?.Invoke("Receipt");
    private void OnRepledgeClick(object sender, RoutedEventArgs e) => _navigationCallback?.Invoke("Repledge");
    private void OnNoticesClick(object sender, RoutedEventArgs e) => _navigationCallback?.Invoke("Notices");
    private void OnReportsClick(object sender, RoutedEventArgs e) => _navigationCallback?.Invoke("Reports");
    private void OnDayBookClick(object sender, RoutedEventArgs e) => _navigationCallback?.Invoke("DayBook");
}

