using System.IO;
using System.Windows;
using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Infrastructure.Persistence;
using JGF.Infrastructure.Persistence.Seed;
using JGF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace JGF.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDirectory, "jgf-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .UseSerilog()
                .ConfigureServices((ctx, services) =>
                {
                    var connStr = ctx.Configuration.GetConnectionString("DefaultConnection") 
                                  ?? "Host=localhost;Port=5432;Database=jgf_db;Username=postgres;Password=postgres;SSL Mode=Prefer";

                    services.AddDbContext<JgfDbContext>(opts =>
                    {
                        opts.UseNpgsql(connStr);
                    });

                    // Core Security & Auth
                    services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
                    services.AddSingleton<IPermissionService, PermissionService>();
                    services.AddSingleton<IAesEncryptionService, AesEncryptionService>();
                    services.AddScoped<IAuthService, AuthService>();
                    services.AddScoped<IAuditService, AuditService>();
                    services.AddScoped<IBackupService, BackupService>();

                    // Master services
                    services.AddScoped<MasterDataService>();
                    services.AddScoped<ICustomerService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddScoped<ISchemeService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddScoped<IJewelTypeService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddScoped<IGoldRateService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddScoped<IBankService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddScoped<IAccountingService>(sp => sp.GetRequiredService<MasterDataService>());
                    services.AddScoped<INumberSeriesService>(sp => sp.GetRequiredService<MasterDataService>());

                    // ViewModels & Views
                    services.AddSingleton<Views.MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            // Run database initialization & seed
            using (var scope = _host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<JgfDbContext>();
                try
                {
                    await DbInitializer.SeedAsync(db);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize or seed database. Check connection settings.");
                }
            }

            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed.");
            System.Windows.MessageBox.Show($"Application failed to start: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

