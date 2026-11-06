using System.Windows;
using JGF.Application.Accounting;
using JGF.Application.Dashboard;
using JGF.Application.Loans;
using JGF.Application.Notices;
using JGF.Application.Receipts;
using JGF.Application.Reports;
using JGF.Application.Repledge;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JGF.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IPermissionService _permissionService;
    private User? _currentUser;

    public MainWindow(IServiceProvider services, IPermissionService permissionService)
    {
        InitializeComponent();
        _services = services;
        _permissionService = permissionService;

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var authService = _services.GetRequiredService<IAuthService>();
        if (await authService.IsFirstRunAsync())
        {
            var wizard = new FirstRunWizardDialog(authService);
            if (wizard.ShowDialog() != true)
            {
                Close();
                return;
            }
        }

        // Prompt Login
        var loginDlg = new LoginDialog(authService);
        if (loginDlg.ShowDialog() != true || loginDlg.AuthenticatedUser == null)
        {
            Close();
            return;
        }

        _currentUser = loginDlg.AuthenticatedUser;
        ApplyUserSession(_currentUser);
    }

    private void ApplyUserSession(User user)
    {
        TxtCurrentUser.Text = $"{user.FullName} ({user.Username})";
        TxtCurrentRole.Text = user.Role.ToString();

        // Configure Role-Based Menu Visibility
        MenuAuditLog.Visibility = (user.Role == UserRole.Owner) ? Visibility.Visible : Visibility.Collapsed;
        MenuRepledge.Visibility = _permissionService.HasPermission(user.Role, Permission.ManageBankRepledge) ? Visibility.Visible : Visibility.Collapsed;
        MenuNoticesAuction.Visibility = _permissionService.HasPermission(user.Role, Permission.ManageAuction) ? Visibility.Visible : Visibility.Collapsed;
        MenuAccounts.Visibility = _permissionService.HasPermission(user.Role, Permission.ViewAccounts) ? Visibility.Visible : Visibility.Collapsed;
        MenuSettings.Visibility = (user.Role == UserRole.Owner || user.Role == UserRole.Manager) ? Visibility.Visible : Visibility.Collapsed;

        // Check recent backup warning
        _ = CheckBackupStatusAsync();

        // Default open Executive Dashboard as home screen
        OnDashboardClick(this, new RoutedEventArgs());
    }

    private async Task CheckBackupStatusAsync()
    {
        var backupService = _services.GetRequiredService<IBackupService>();
        var isRecent = await backupService.HasRecentSuccessfulBackupAsync(TimeSpan.FromHours(24));
        if (!isRecent)
        {
            TxtBackupWarning.Text = "⚠ WARNING: No successful backup in last 24h!";
            TxtBackupWarning.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    private void OnCustomerMasterClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.CustomerMasterControl(_services.GetRequiredService<ICustomerService>());
        MainContentArea.Content = view;
    }

    private void OnSchemesMasterClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.SchemeMasterControl(_services.GetRequiredService<ISchemeService>());
        MainContentArea.Content = view;
    }

    private void OnJewelTypesClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.JewelTypeMasterControl(_services.GetRequiredService<IJewelTypeService>());
        MainContentArea.Content = view;
    }

    private void OnGoldRateClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.GoldRateMasterControl(_services.GetRequiredService<IGoldRateService>());
        MainContentArea.Content = view;
    }

    private void OnBanksMasterClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.BankMasterControl(_services.GetRequiredService<IBankService>());
        MainContentArea.Content = view;
    }

    private void OnLedgerHeadsClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.LedgerHeadMasterControl(_services.GetRequiredService<IAccountingService>());
        MainContentArea.Content = view;
    }

    private void OnNumberSeriesClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.NumberSeriesMasterControl(_services.GetRequiredService<INumberSeriesService>());
        MainContentArea.Content = view;
    }

    private void OnAuditLogClick(object sender, RoutedEventArgs e)
    {
        if (_currentUser?.Role != UserRole.Owner)
        {
            MessageBox.Show("Audit Log is restricted to Owner only.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var view = new Controls.AuditLogControl(_services.GetRequiredService<IAuditService>());
        MainContentArea.Content = view;
    }

    private void OnBackupSettingsClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.BackupSettingsControl(_services.GetRequiredService<IBackupService>());
        MainContentArea.Content = view;
    }

    private void OnDbSettingsClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.DbConnectionSettingsControl();
        MainContentArea.Content = view;
    }

    private void OnNewLoanClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.NewLoanEntryControl(
            _services.GetRequiredService<ILoanService>(),
            _services.GetRequiredService<ISchemeService>(),
            _services.GetRequiredService<ICustomerService>(),
            _services.GetRequiredService<IGoldRateService>(),
            _services.GetRequiredService<IJewelTypeService>());
        MainContentArea.Content = view;
    }

    private void OnReceiptClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.LoanReceiptControl(
            _services.GetRequiredService<ILoanService>(),
            _services.GetRequiredService<IReceiptService>(),
            _services.GetRequiredService<IInterestCalculator>());
        MainContentArea.Content = view;
    }

    private void OnCustomerDetailClick(object sender, RoutedEventArgs e) => OnCustomerMasterClick(sender, e);
    private void OnRepledgeClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.BankRepledgeControl(
            _services.GetRequiredService<IBankRepledgeService>(),
            _services.GetRequiredService<IBankService>());
        MainContentArea.Content = view;
    }

    private void OnNoticesAuctionClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.NoticeAuctionControl(
            _services.GetRequiredService<IOverdueNoticeService>(),
            _currentUser?.Id,
            _currentUser?.Role);
        MainContentArea.Content = view;
    }

    private void OnDashboardClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.DashboardControl(
            _services.GetRequiredService<IDashboardService>(),
            target =>
            {
                switch (target)
                {
                    case "NewLoan": OnNewLoanClick(this, new RoutedEventArgs()); break;
                    case "Receipt": OnReceiptClick(this, new RoutedEventArgs()); break;
                    case "Repledge": OnRepledgeClick(this, new RoutedEventArgs()); break;
                    case "Notices": OnNoticesAuctionClick(this, new RoutedEventArgs()); break;
                    case "Reports": OnReportsClick(this, new RoutedEventArgs()); break;
                    case "DayBook": OnDayBookClick(this, new RoutedEventArgs()); break;
                }
            });
        MainContentArea.Content = view;
    }

    private void OnReportsClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.JlReportsControl(
            _services.GetRequiredService<IJlReportService>(),
            _services.GetRequiredService<ISchemeService>(),
            _services.GetRequiredService<ICustomerService>(),
            _services.GetRequiredService<IExcelExportService>(),
            _currentUser?.Id,
            _currentUser?.Role ?? UserRole.Owner);
        MainContentArea.Content = view;
    }

    private void OnFinancialReportsClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.FinancialReportsControl(_services.GetRequiredService<IAccountingReportService>());
        MainContentArea.Content = view;
    }

    private void OnIntegrityCheckClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.AccountsIntegrityControl(_services.GetRequiredService<IAccountingReportService>());
        MainContentArea.Content = view;
    }

    private void OnStockClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.LoanHistoryControl(_services.GetRequiredService<ILoanService>());
        MainContentArea.Content = view;
    }

    private void OnDayBookClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.DayBookEntryControl(
            _services.GetRequiredService<IAccountingReportService>(),
            _services.GetRequiredService<IAccountingService>());
        MainContentArea.Content = view;
    }

    private void OnVouchersClick(object sender, RoutedEventArgs e) => OnDayBookClick(sender, e);

    private void OnDayCloseClick(object sender, RoutedEventArgs e)
    {
        var view = new Controls.DayEndCloseControl(_services.GetRequiredService<IDayCloseService>());
        MainContentArea.Content = view;
    }
    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}

