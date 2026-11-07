using System.Windows;
using System.Windows.Controls;
using JGF.Application.Repledge;
using JGF.Application.Services;

namespace JGF.Desktop.Views.Controls;

public partial class BankRepledgeControl : UserControl
{
    private readonly IBankRepledgeService _repledgeService;
    private readonly IBankService _bankService;

    private readonly BankRepledgeNewEntryControl _newEntryControl;
    private readonly BankPaymentControl _paymentControl;
    private readonly BankReportsControl _reportsControl;

    public BankRepledgeControl(
        IBankRepledgeService repledgeService,
        IBankService bankService)
    {
        InitializeComponent();
        _repledgeService = repledgeService;
        _bankService = bankService;

        // Instantiate child controls
        _newEntryControl = new BankRepledgeNewEntryControl(_repledgeService, _bankService);
        _paymentControl = new BankPaymentControl(_repledgeService);
        _reportsControl = new BankReportsControl(_repledgeService);

        NewEntryContentHost.Content = _newEntryControl;
        PaymentContentHost.Content = _paymentControl;
        ReportsContentHost.Content = _reportsControl;

        Loaded += async (s, e) => await LoadPledgesAsync();
    }

    private async Task LoadPledgesAsync()
    {
        try
        {
            var list = await _repledgeService.GetAllBankPledgesAsync();
            GridPledges.ItemsSource = list;

            var dueSoonCount = list.Count(p => p.IsDueSoon);
            var overdueCount = list.Count(p => p.IsOverdue);
            if (overdueCount > 0 || dueSoonCount > 0)
            {
                TxtDashboardNotice.Text = $"⚠ Action Required: {overdueCount} bank pledges OVERDUE! {dueSoonCount} bank pledges due within 15 days.";
                TxtDashboardNotice.Foreground = System.Windows.Media.Brushes.Crimson;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load bank pledges: {ex.Message}", "Bank Repledge Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnPledgeSelected(object sender, SelectionChangedEventArgs e)
    {
        // Selection feedback
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadPledgesAsync();
    }

    private void OnGoToNewPledgeTabClick(object sender, RoutedEventArgs e)
    {
        RepledgeTabControl.SelectedIndex = 1;
    }

    private void OnMakePaymentForPledgeClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BankPledgeSummaryDto p)
        {
            _paymentControl.TxtSearchQuery.Text = p.PledgeNumber;
            RepledgeTabControl.SelectedIndex = 2; // Switch to Payment tab
        }
    }
}
