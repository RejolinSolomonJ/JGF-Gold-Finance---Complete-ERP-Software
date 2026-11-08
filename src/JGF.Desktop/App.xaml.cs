using System.IO;
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
                            npgsqlOpts.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(5),
                                errorCodesToAdd: null);
                            npgsqlOpts.CommandTimeout(15);
                        });
                    });

                    // Core Security & Auth
                    services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
                    services.AddSingleton<IPermissionService, PermissionService>();
                    services.AddSingleton<IAesEncryptionService, AesEncryptionService>();
                    services.AddTransient<IAuthService, AuthService>();
                    services.AddTransient<IAuditService, AuditService>();
                    services.AddTransient<IBackupService, BackupService>();

                    // Master services
                    services.AddTransient<MasterDataService>();
                    services.AddTransient<ICustomerService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<ISchemeService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IJewelTypeService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IGoldRateService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IBankService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<IAccountingService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddTransient<INumberSeriesService>(sp => sp.GetRequiredService<MasterDataService>());

                    // Accounting & Reporting services
                    services.AddTransient<IAccountingReportService, AccountingReportService>();
                    services.AddTransient<IDayCloseService, DayCloseService>();
                    services.AddTransient<IBankRepledgeService, BankRepledgeService>();
                    services.AddTransient<IExcelExportService, ExcelExportService>();
                    services.AddTransient<IJlReportService, JlReportService>();
                    services.AddTransient<IDashboardService, DashboardService>();

                    // Loan & Receipt processing services
                    services.AddSingleton<IInterestCalculator, StandardInterestCalculator>();
                    services.AddTransient<ILoanService, LoanService>();
                    services.AddTransient<IReceiptService, ReceiptService>();
                    services.AddTransient<IOverdueNoticeService, OverdueNoticeService>();
                    services.AddTransient<JGF.Application.Migration.IDataMigrationService, DataMigrationService>();
                    services.AddTransient<ISafeUpdateService, SafeUpdateService>();

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
}
