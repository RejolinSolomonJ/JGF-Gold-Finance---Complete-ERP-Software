using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Serilog;

namespace JGF.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public static readonly RoutedUICommand CopyEntireRowCommand = new RoutedUICommand(
        "Copy Entire Row", "CopyEntireRow", typeof(App));

    public static string ProgramDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "JGF");

    public static string GetEffectiveConfigPath()
    {
        var prodConfig = Path.Combine(ProgramDataDirectory, "appsettings.json");
        return File.Exists(prodConfig) ? prodConfig : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Initialize Production ProgramData Directories & Serilog Logging
        string logDirectory;
        try
        {
            var dataDir = ProgramDataDirectory;
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(Path.Combine(dataDir, "Logs"));
            Directory.CreateDirectory(Path.Combine(dataDir, "Backups"));
            Directory.CreateDirectory(Path.Combine(dataDir, "Data"));
            logDirectory = Path.Combine(dataDir, "Logs");
        }
        catch
        {
            logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "jgf-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== JGF Jewel Gold Finance Desktop Application Starting ===");

        // Register Global DataGrid Behaviors (Universal Cell/Row Copyability via Ctrl+C & Right Click)
        RegisterGlobalDataGridBehaviors();

        // 2. Register Global Exception Handlers to Prevent "Not Responding" and Sudden Crashes
        DispatcherUnhandledException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI Dispatcher Exception caught: {Message}", args.Exception.Message);
            args.Handled = true; // Keep app running!

            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe application has safely recovered and remains open. Detailed diagnostics have been logged to the Logs folder.",
                "JGF — Error Recovered",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "AppDomain Unhandled Exception (IsTerminating={IsTerminating})", args.IsTerminating);
            }
            else
            {
                Log.Fatal("AppDomain Unhandled Exception: {ExceptionObject}", args.ExceptionObject);
            }
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Log.Error(args.Exception, "TaskScheduler Unobserved Task Exception caught: {Message}", args.Exception.Message);
            args.SetObserved(); // Prevent crashing the process on GC collection
        };

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    // 1. Base default application configuration from install directory
                    cfg.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

                    // 2. Production overlay configuration from C:\ProgramData\JGF\appsettings.json
                    try
                    {
                        var prodConfigPath = Path.Combine(ProgramDataDirectory, "appsettings.json");
                        if (File.Exists(prodConfigPath))
                        {
                            cfg.AddJsonFile(prodConfigPath, optional: true, reloadOnChange: true);
                        }
                    }
                    catch { }
                })
                .UseSerilog()
                .ConfigureServices((ctx, services) =>
                {
                    var rawConnStr = ctx.Configuration.GetConnectionString("DefaultConnection") 
                                  ?? "Host=localhost;Port=5432;Database=jgf_db;Username=postgres;Password=postgres;SSL Mode=Prefer";

                    // Guarantee Timeout=5 and Command Timeout=15 are present
                    var builder = new NpgsqlConnectionStringBuilder(rawConnStr)
                    {
                        Timeout = 5,
                        CommandTimeout = 15
                    };
                    var connStr = builder.ConnectionString;

                    // Register IDbContextFactory for thread-safe, isolated, short-lived DbContexts
                    services.AddDbContextFactory<JgfDbContext>(opts =>
                    {
                        opts.UseNpgsql(connStr, npgsqlOpts =>
                        {
                            npgsqlOpts.CommandTimeout(15);
                        });
                    });

                    // Core Security & Auth
                    services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
                    services.AddSingleton<IPermissionService, PermissionService>();
                    services.AddSingleton<IAesEncryptionService, AesEncryptionService>();
                    services.AddTransient<IAuthService>(sp => new AuthService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<IPasswordHasher>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AuthService>>()));
                    services.AddTransient<IAuditService>(sp => new AuditService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>()));
                    services.AddTransient<IBackupService>(sp => new BackupService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<IAesEncryptionService>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BackupService>>()));

                    // Master services
                    services.AddTransient<MasterDataService>(sp => new MasterDataService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>()));
                    services.AddTransient<ICustomerService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<ISchemeService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IJewelTypeService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IGoldRateService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IBankService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IAccountingService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<INumberSeriesService>(sp => sp.GetRequiredService<MasterDataService>());

                    // Accounting & Reporting services
                    services.AddTransient<IAccountingReportService>(sp => new AccountingReportService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<INumberSeriesService>(),
                        sp.GetRequiredService<IPermissionService>()));
                    services.AddTransient<IDayCloseService>(sp => new DayCloseService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<IAccountingReportService>(),
                        sp.GetRequiredService<IBackupService>(),
                        sp.GetRequiredService<IPermissionService>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DayCloseService>>()));
                    services.AddTransient<IBankRepledgeService>(sp => new BankRepledgeService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<INumberSeriesService>(),
                        sp.GetRequiredService<IPermissionService>()));
                    services.AddTransient<IExcelExportService>(sp => new ExcelExportService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>()));
                    services.AddTransient<IJlReportService, JlReportService>();
                    services.AddTransient<IDashboardService>(sp => new DashboardService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<IBackupService>()));

                    // Loan & Receipt processing services
                    services.AddSingleton<IInterestCalculator, StandardInterestCalculator>();
                    services.AddTransient<ILoanService>(sp => new LoanService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<INumberSeriesService>(),
                        sp.GetRequiredService<IPermissionService>()));
                    services.AddTransient<IReceiptService>(sp => new ReceiptService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<INumberSeriesService>(),
                        sp.GetRequiredService<IPermissionService>()));
                    services.AddTransient<IOverdueNoticeService>(sp => new OverdueNoticeService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<IInterestCalculator>(),
                        sp.GetRequiredService<INumberSeriesService>(),
                        sp.GetRequiredService<IPermissionService>()));
                    services.AddTransient<JGF.Application.Migration.IDataMigrationService>(sp => new DataMigrationService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<IAccountingReportService>()));
                    services.AddTransient<ISafeUpdateService>(sp => new SafeUpdateService(
                        sp.GetRequiredService<IDbContextFactory<JgfDbContext>>(),
                        sp.GetRequiredService<IBackupService>(),
                        sp.GetRequiredService<IAesEncryptionService>(),
                        sp.GetRequiredService<IAccountingReportService>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SafeUpdateService>>()));

                    // Main Shell Window
                    services.AddSingleton<Views.MainWindow>();
                })
                .Build();

            _host.Start();

            // Show MainWindow immediately! (Background pre-flight checks handle DB initialization safely)
            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application fatal start-up error.");
            MessageBox.Show($"Application failed to start: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("=== JGF Jewel Gold Finance Desktop Application Exiting ===");
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    #region Universal DataGrid Copyability & Accessibility
    private static void RegisterGlobalDataGridBehaviors()
    {
        // 1. Right-click on any DataGridCell focuses and selects it immediately
        EventManager.RegisterClassHandler(
            typeof(DataGridCell),
            UIElement.PreviewMouseRightButtonDownEvent,
            new MouseButtonEventHandler((sender, _) =>
            {
                if (sender is DataGridCell cell)
                {
                    cell.Focus();
                    cell.IsSelected = true;
                }
            }));

        // 2. Class CommandBinding for ApplicationCommands.Copy on DataGridCell (Cell Copy / Ctrl+C)
        CommandManager.RegisterClassCommandBinding(
            typeof(DataGridCell),
            new CommandBinding(ApplicationCommands.Copy, (sender, e) =>
            {
                if (sender is not DataGridCell cell) return;
                var grid = FindVisualParent<DataGrid>(cell);

                // If user selected multiple cells across rows, let default WPF DataGrid multi-cell copy run
                if (grid != null && grid.SelectedCells.Count > 1)
                {
                    return; // Bubbles up to DataGrid built-in copy
                }

                var text = ExtractCellText(cell);
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        Clipboard.SetDataObject(text, true);
                        e.Handled = true;
                    }
                    catch
                    {
                        // Fallback to standard command routing
                    }
                }
            },
            (sender, e) =>
            {
                e.CanExecute = true;
                e.Handled = true;
            }));

        // 3. Class CommandBinding for CopyEntireRowCommand on DataGridCell
        CommandManager.RegisterClassCommandBinding(
            typeof(DataGridCell),
            new CommandBinding(CopyEntireRowCommand, (sender, e) =>
            {
                if (sender is not DataGridCell cell) return;
                var grid = FindVisualParent<DataGrid>(cell);
                if (grid == null) return;

                var rowItem = cell.DataContext;
                if (rowItem == null) return;

                var values = new List<string>();
                foreach (var col in grid.Columns.OrderBy(c => c.DisplayIndex))
                {
                    if (col.Visibility != Visibility.Visible) continue;
                    var val = ExtractColumnValue(col, rowItem);
                    if (!string.IsNullOrEmpty(val))
                    {
                        values.Add(val.Replace("\r", "").Replace("\n", " ").Trim());
                    }
                }

                if (values.Count > 0)
                {
                    var rowLine = string.Join("\t", values);
                    try
                    {
                        Clipboard.SetDataObject(rowLine, true);
                        e.Handled = true;
                    }
                    catch { }
                }
            },
            (sender, e) =>
            {
                e.CanExecute = true;
                e.Handled = true;
            }));
    }

    private static string ExtractCellText(DataGridCell cell)
    {
        // 1. Direct TextBlock
        if (cell.Content is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
        {
            return tb.Text.Trim();
        }

        // 2. FrameworkElement containing TextBlocks (e.g. TemplateColumns with badges or formatted text)
        if (cell.Content is FrameworkElement fe)
        {
            var textBlocks = FindVisualChildren<TextBlock>(fe).ToList();
            if (textBlocks.Count > 0)
            {
                var combined = string.Join(" ", textBlocks.Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
                if (!string.IsNullOrEmpty(combined)) return combined.Trim();
            }
        }

        // 3. DataGridBoundColumn binding
        if (cell.Column is DataGridBoundColumn boundCol && boundCol.Binding is Binding b && cell.DataContext != null)
        {
            var propPath = b.Path?.Path;
            if (!string.IsNullOrEmpty(propPath))
            {
                var prop = cell.DataContext.GetType().GetProperty(propPath);
                var val = prop?.GetValue(cell.DataContext);
                if (val != null)
                {
                    return !string.IsNullOrEmpty(b.StringFormat)
                        ? string.Format(b.StringFormat, val).Trim()
                        : val.ToString()?.Trim() ?? "";
                }
            }
        }

        // 4. Column ClipboardContentBinding
        if (cell.Column.ClipboardContentBinding is Binding ccb && cell.DataContext != null)
        {
            var propPath = ccb.Path?.Path;
            if (!string.IsNullOrEmpty(propPath))
            {
                var prop = cell.DataContext.GetType().GetProperty(propPath);
                var val = prop?.GetValue(cell.DataContext);
                if (val != null) return val.ToString()?.Trim() ?? "";
            }
        }

        return cell.Content?.ToString()?.Trim() ?? "";
    }

    private static string ExtractColumnValue(DataGridColumn col, object rowItem)
    {
        if (col is DataGridBoundColumn boundCol && boundCol.Binding is Binding b)
        {
            var propPath = b.Path?.Path;
            if (!string.IsNullOrEmpty(propPath))
            {
                var prop = rowItem.GetType().GetProperty(propPath);
                var val = prop?.GetValue(rowItem);
                if (val != null)
                {
                    return !string.IsNullOrEmpty(b.StringFormat)
                        ? string.Format(b.StringFormat, val)
                        : val.ToString() ?? "";
                }
            }
        }

        if (col.ClipboardContentBinding is Binding ccb)
        {
            var propPath = ccb.Path?.Path;
            if (!string.IsNullOrEmpty(propPath))
            {
                var prop = rowItem.GetType().GetProperty(propPath);
                var val = prop?.GetValue(rowItem);
                if (val != null) return val.ToString() ?? "";
            }
        }

        return "";
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObj = VisualTreeHelper.GetParent(child);
        while (parentObj != null)
        {
            if (parentObj is T parent) return parent;
            parentObj = VisualTreeHelper.GetParent(parentObj);
        }
        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        int count = VisualTreeHelper.GetChildrenCount(depObj);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t) yield return t;
            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }
    #endregion
}
