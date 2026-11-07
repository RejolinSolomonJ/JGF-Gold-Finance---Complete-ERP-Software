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
using JGF.Application.Updates;
using JGF.Desktop.Views.Controls;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog;

namespace JGF.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IPermissionService _permissionService;
    private User? _currentUser;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private readonly System.Windows.Threading.DispatcherTimer _inactivityTimer = new();
    private bool _isScreenLocked;

    public MainWindow(IServiceProvider services, IPermissionService permissionService)
    {
        InitializeComponent();
        _services = services;
        _permissionService = permissionService;

        Loaded += async (s, e) => await InitializeAsync();

        // In-app Security Screen Lock Setup
        PreviewMouseMove += (s, e) => ResetInactivity();
        PreviewKeyDown += (s, e) => ResetInactivity();
        PreviewMouseDown += (s, e) => ResetInactivity();

        _inactivityTimer.Interval = TimeSpan.FromSeconds(30);
        _inactivityTimer.Tick += OnInactivityTick;
        _inactivityTimer.Start();
    }

    private void ResetInactivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    private void OnInactivityTick(object? sender, EventArgs e)
    {
        if (_currentUser != null && !_isScreenLocked && DateTime.UtcNow - _lastActivityUtc >= TimeSpan.FromMinutes(10))
        {
            LockScreen();
        }
    }

    private void LockScreen()
    {
        if (_currentUser == null || _isScreenLocked) return;

        _isScreenLocked = true;
        try
        {
            var hasher = _services.GetRequiredService<IPasswordHasher>();
            var lockDlg = new ScreenLockDialog(_currentUser, hasher);
            var unlocked = lockDlg.ShowDialog();

            if (lockDlg.ShouldLogout || unlocked != true)
            {
                // Restart login flow
                _currentUser = null;
                MainContentArea.Content = null;
                _ = InitializeAsync();
            }
        }
        finally
        {
            _isScreenLocked = false;
            ResetInactivity();
        }
    }

    private async Task InitializeAsync()
    {
        await TryConnectAndInitializeAsync();
    }

    private async Task TryConnectAndInitializeAsync()
    {
        // 1. Determine connection parameters
        var config = _services.GetRequiredService<IConfiguration>();
        var connStr = config.GetConnectionString("DefaultConnection") 
                      ?? "Host=localhost;Port=5432;Database=jgf_db;Username=postgres;Password=postgres;SSL Mode=Prefer;Timeout=5;Command Timeout=15;";

        var builder = new NpgsqlConnectionStringBuilder(connStr);
        var host = string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host;
        var port = builder.Port > 0 ? builder.Port : 5432;

        // 2. Show background startup progress
        var progress = new StartupProgressControl($"Testing connection to PostgreSQL database ({host}:{port})...");
        MainContentArea.Content = progress;

        bool canConnect = false;
        string? errorMessage = null;

        try
        {
            var dbFactory = _services.GetRequiredService<IDbContextFactory<JgfDbContext>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));

            await Task.Run(async () =>
            {
                await using var testDb = await dbFactory.CreateDbContextAsync(cts.Token);
                canConnect = await testDb.Database.CanConnectAsync(cts.Token);
            }, cts.Token);
        }
        catch (Exception ex)
        {
            canConnect = false;
            errorMessage = ex.Message;
            Log.Warning(ex, "Database connection test failed for {Host}:{Port}", host, port);
        }

        // 3. Handle Connection Failure Gracefully
        if (!canConnect)
        {
            TxtDbStatus.Text = "● DB: Disconnected (Check Settings)";
            TxtDbStatus.Foreground = System.Windows.Media.Brushes.Red;

            var failureControl = new ConnectionFailureControl(host, port, errorMessage ?? "Connection attempt timed out after 5 seconds.");
            failureControl.RetryRequested += async (s, e) => await TryConnectAndInitializeAsync();
            failureControl.OpenSettingsRequested += (s, e) => OnDbSettingsClick(this, new RoutedEventArgs());
            MainContentArea.Content = failureControl;
            return;
        }

        // 4. Connection Succeeded - Initialize & Seed in Background
        TxtDbStatus.Text = "● DB: Connected (LAN PC1)";
        TxtDbStatus.Foreground = System.Windows.Media.Brushes.LightGreen;

        progress.UpdateStatus("Verifying database schema, security rules and baseline master data...");

        try
        {
            var dbFactory = _services.GetRequiredService<IDbContextFactory<JgfDbContext>>();
            await Task.Run(async () =>
            {
                await using var db = await dbFactory.CreateDbContextAsync();
                await DbInitializer.SeedAsync(db);
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Background database initialization encountered an error.");
        }

        // 5. Versioning & First Run Check
        try
        {
            var authService = _services.GetRequiredService<IAuthService>();
            var updateService = _services.GetRequiredService<ISafeUpdateService>();

            if (await authService.IsFirstRunAsync())
            {
                var wizard = new FirstRunWizardDialog(authService) { Owner = this };
                if (wizard.ShowDialog() != true)
                {
                    Close();
                    return;
                }
            }

            var versionStatus = await updateService.CheckVersionStatusAsync();
            if (versionStatus.Status == VersionComparisonResult.AppOlderThanDb)
            {
                MessageBox.Show(
                    $"Please update this PC to version {versionStatus.DatabaseVersion.Raw}.\n\n" +
                    $"Current Application Version: {versionStatus.CurrentAppVersion.Raw}\n" +
                    $"Database Schema Version: {versionStatus.DatabaseVersion.Raw}\n\n" +
                    "The database has been updated. You must install the latest JGF update before using this workstation.",
                    "Update Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);
                Close();
                return;
            }

            // Prompt Login immediately
            var loginDlg = new LoginDialog(authService) { Owner = this };
            if (loginDlg.ShowDialog() != true || loginDlg.AuthenticatedUser == null)
            {
                Close();
                return;
            }

            _currentUser = loginDlg.AuthenticatedUser;

            if (versionStatus.Status == VersionComparisonResult.AppNewerThanDb)
            {
                if (_currentUser.Role == UserRole.Cashier)
                {
                    MessageBox.Show(
                        "System update pending, contact the owner.\n\n" +
                        $"The database must be upgraded to version {versionStatus.CurrentAppVersion.Raw} by the Owner or Manager before cashier operations can proceed.",
                        "System Update Pending",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Close();
                    return;
                }
                else
                {
                    var askUpgrade = MessageBox.Show(
                        $"Database schema upgrade required!\n\n" +
                        $"Database Version: {versionStatus.DatabaseVersion.Raw}\n" +
                        $"Application Version: {versionStatus.CurrentAppVersion.Raw}\n\n" +
                        "Would you like to open the Safe Update Wizard now to perform the verified pre-update backup and schema upgrade?",
                        "Database Update Pending",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (askUpgrade == MessageBoxResult.Yes)
                    {
                        var wizard = new SafeUpdateWizardDialog(updateService, _services) { Owner = this };
                        wizard.ShowDialog();
                    }
                }
            }

            var workstationName = Environment.MachineName.ToLowerInvariant().Contains("pc2") ? "PC2" : "PC1";
            await updateService.RecordWorkstationHeartbeatAsync(workstationName, _currentUser.Username);

            ApplyUserSession(_currentUser);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during authentication or startup sequencing.");
            MessageBox.Show($"Startup error: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyUserSession(User user)
    {
        TxtCurrentUser.Text = $"{user.FullName} ({user.Username})";
        TxtCurrentRole.Text = user.Role.ToString();

        // Configure Role-Based Menu Visibility
        MenuAuditLog.Visibility = (user.Role == UserRole.Owner) ? Visibility.Visible : Visibility.Collapsed;
        MenuDataMigration.Visibility = (user.Role == UserRole.Owner) ? Visibility.Visible : Visibility.Collapsed;
        MenuRepledge.Visibility = _permissionService.HasPermission(user.Role, Permission.ManageBankRepledge) ? Visibility.Visible : Visibility.Collapsed;
        MenuNoticesAuction.Visibility = _permissionService.HasPermission(user.Role, Permission.ManageAuction) ? Visibility.Visible : Visibility.Collapsed;
        MenuAccounts.Visibility = _permissionService.HasPermission(user.Role, Permission.ViewAccounts) ? Visibility.Visible : Visibility.Collapsed;
        MenuSettings.Visibility = (user.Role == UserRole.Owner || user.Role == UserRole.Manager) ? Visibility.Visible : Visibility.Collapsed;
        MenuSafeUpdate.Visibility = (user.Role == UserRole.Owner || user.Role == UserRole.Manager) ? Visibility.Visible : Visibility.Collapsed;

        // Check recent backup warning in background safely
        Task.Run(async () => await CheckBackupStatusAsync());

        // Default open Executive Dashboard as home screen
        OnDashboardClick(this, new RoutedEventArgs());
    }

    private async Task CheckBackupStatusAsync()
    {
        try
        {
            var backupService = _services.GetRequiredService<IBackupService>();
            var isRecent = await backupService.HasRecentSuccessfulBackupAsync(TimeSpan.FromHours(24));
            await Dispatcher.InvokeAsync(() =>
            {
                if (!isRecent)
                {
                    TxtBackupWarning.Text = "⚠ WARNING: No successful backup in last 24h!";
                    TxtBackupWarning.Foreground = System.Windows.Media.Brushes.OrangeRed;
                }
                else
                {
                    TxtBackupWarning.Text = "Backup: Up to date";
                    TxtBackupWarning.Foreground = System.Windows.Media.Brushes.DarkGray;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to query backup status.");
        }
    }

    private void SafeNavigate(Action navigationAction)
    {
        try
        {
            navigationAction();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Navigation failed.");
            MessageBox.Show($"Failed to open screen: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCustomerMasterClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.CustomerMasterControl(_services.GetRequiredService<ICustomerService>());
        MainContentArea.Content = view;
    });

    private void OnSchemesMasterClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.SchemeMasterControl(_services.GetRequiredService<ISchemeService>());
        MainContentArea.Content = view;
    });

    private void OnJewelTypesClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.JewelTypeMasterControl(_services.GetRequiredService<IJewelTypeService>());
        MainContentArea.Content = view;
    });

    private void OnGoldRateClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.GoldRateMasterControl(_services.GetRequiredService<IGoldRateService>());
        MainContentArea.Content = view;
    });

    private void OnBanksMasterClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.BankMasterControl(_services.GetRequiredService<IBankService>());
        MainContentArea.Content = view;
    });

    private void OnLedgerHeadsClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.LedgerHeadMasterControl(_services.GetRequiredService<IAccountingService>());
        MainContentArea.Content = view;
    });

    private void OnNumberSeriesClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.NumberSeriesMasterControl(_services.GetRequiredService<INumberSeriesService>());
        MainContentArea.Content = view;
    });

    private void OnAuditLogClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        if (_currentUser?.Role != UserRole.Owner)
        {
            MessageBox.Show("Audit Log is restricted to Owner only.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var view = new Controls.AuditLogControl(_services.GetRequiredService<IAuditService>());
        MainContentArea.Content = view;
    });

    private void OnDataMigrationClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        if (_currentUser?.Role != UserRole.Owner)
        {
            MessageBox.Show("Data Migration is restricted to Owner only.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var view = new Controls.DataMigrationControl(_services.GetRequiredService<JGF.Application.Migration.IDataMigrationService>());
        MainContentArea.Content = view;
    });

    private void OnBackupSettingsClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.BackupSettingsControl(_services.GetRequiredService<IBackupService>());
        MainContentArea.Content = view;
    });

    private void OnDbSettingsClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var dbSettings = new Controls.DbConnectionSettingsControl();
        dbSettings.ConnectionConfigured += async (s, args) =>
        {
            await TryConnectAndInitializeAsync();
        };
        MainContentArea.Content = dbSettings;
    });

    private void OnNewLoanClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.NewLoanEntryControl(
            _services.GetRequiredService<ILoanService>(),
            _services.GetRequiredService<ISchemeService>(),
            _services.GetRequiredService<ICustomerService>(),
            _services.GetRequiredService<IGoldRateService>(),
            _services.GetRequiredService<IJewelTypeService>());
        MainContentArea.Content = view;
    });

    private void OnReceiptClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.LoanReceiptControl(
            _services.GetRequiredService<ILoanService>(),
            _services.GetRequiredService<IReceiptService>(),
            _services.GetRequiredService<IInterestCalculator>());
        MainContentArea.Content = view;
    });

    private void OnCustomerDetailClick(object sender, RoutedEventArgs e) => OnCustomerMasterClick(sender, e);

    private void OnRepledgeClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.BankRepledgeControl(
            _services.GetRequiredService<IBankRepledgeService>(),
            _services.GetRequiredService<IBankService>());
        MainContentArea.Content = view;
    });

    private void OnNoticesAuctionClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.NoticeAuctionControl(
            _services.GetRequiredService<IOverdueNoticeService>(),
            _currentUser?.Id,
            _currentUser?.Role);
        MainContentArea.Content = view;
    });

    private void OnDashboardClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
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
    });

    private void OnReportsClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.JlReportsControl(
            _services.GetRequiredService<IJlReportService>(),
            _services.GetRequiredService<ISchemeService>(),
            _services.GetRequiredService<ICustomerService>(),
            _services.GetRequiredService<IExcelExportService>(),
            _currentUser?.Id,
            _currentUser?.Role ?? UserRole.Owner);
        MainContentArea.Content = view;
    });

    private void OnFinancialReportsClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.FinancialReportsControl(_services.GetRequiredService<IAccountingReportService>());
        MainContentArea.Content = view;
    });

    private void OnIntegrityCheckClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.AccountsIntegrityControl(_services.GetRequiredService<IAccountingReportService>());
        MainContentArea.Content = view;
    });

    private void OnStockClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.LoanHistoryControl(_services.GetRequiredService<ILoanService>());
        MainContentArea.Content = view;
    });

    private void OnDayBookClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.DayBookEntryControl(
            _services.GetRequiredService<IAccountingReportService>(),
            _services.GetRequiredService<IAccountingService>());
        MainContentArea.Content = view;
    });

    private void OnVouchersClick(object sender, RoutedEventArgs e) => OnDayBookClick(sender, e);

    private void OnDayCloseClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var view = new Controls.DayEndCloseControl(_services.GetRequiredService<IDayCloseService>());
        MainContentArea.Content = view;
    });

    private void OnSafeUpdateWizardClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var updateService = _services.GetRequiredService<ISafeUpdateService>();
        var dlg = new SafeUpdateWizardDialog(updateService, _services) { Owner = this };
        dlg.ShowDialog();
    });

    private void OnAboutClick(object sender, RoutedEventArgs e) => SafeNavigate(() =>
    {
        var updateService = _services.GetRequiredService<ISafeUpdateService>();
        var dlg = new AboutDialog(updateService, _services) { Owner = this };
        dlg.ShowDialog();
    });

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
