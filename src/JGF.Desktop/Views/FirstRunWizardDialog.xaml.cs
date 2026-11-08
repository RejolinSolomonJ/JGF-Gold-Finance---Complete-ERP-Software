using System.IO;
using System.Text.Json;
using System.Windows;
using JGF.Application.Security;
using Npgsql;

namespace JGF.Desktop.Views;

public partial class FirstRunWizardDialog : Window
{
    private readonly IAuthService _authService;

    public FirstRunWizardDialog(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        Loaded += OnWizardLoaded;
    }

    private void OnWizardLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = App.GetEffectiveConfigPath();
            if (File.Exists(configPath))
            {
                var text = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csElem) &&
                    csElem.TryGetProperty("DefaultConnection", out var defConn))
                {
                    var connStr = defConn.GetString() ?? "";
                    var builder = new NpgsqlConnectionStringBuilder(connStr);
                    if (!string.IsNullOrEmpty(builder.Host)) TxtDbHost.Text = builder.Host;
                    if (builder.Port > 0) TxtDbPort.Text = builder.Port.ToString();
                    if (!string.IsNullOrEmpty(builder.Database)) TxtDbName.Text = builder.Database;
                    if (!string.IsNullOrEmpty(builder.Username)) TxtDbUser.Text = builder.Username;
                    if (!string.IsNullOrEmpty(builder.Password)) TxtDbPassword.Password = builder.Password;
                }
            }
        }
        catch
        {
            // Default pre-populated values remain
        }
    }

    private string BuildConnectionString()
    {
        var host = TxtDbHost.Text.Trim();
        var port = TxtDbPort.Text.Trim();
        var database = TxtDbName.Text.Trim();
        var user = TxtDbUser.Text.Trim();
        var pass = TxtDbPassword.Password;

        return $"Host={host};Port={port};Database={database};Username={user};Password={pass};SSL Mode=Prefer;Timeout=5;Command Timeout=15;";
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        var host = TxtDbHost.Text.Trim();
        var port = TxtDbPort.Text.Trim();
        var targetDb = TxtDbName.Text.Trim();
        var user = TxtDbUser.Text.Trim();
        var pass = TxtDbPassword.Password;

        var adminConnStr = $"Host={host};Port={port};Database=postgres;Username={user};Password={pass};SSL Mode=Prefer;Timeout=5;";

        using var adminConn = new NpgsqlConnection(adminConnStr);
        await adminConn.OpenAsync();

        using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{targetDb}'", adminConn);
        var exists = await checkCmd.ExecuteScalarAsync();
        if (exists == null)
        {
            using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{targetDb}\"", adminConn);
            await createCmd.ExecuteNonQueryAsync();
        }
    }

    private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
    {
        TxtConnFeedback.Text = "Testing connection to PostgreSQL...";
        BorderConnFeedback.Background = System.Windows.Media.Brushes.DimGray;
        BorderConnFeedback.BorderBrush = System.Windows.Media.Brushes.Goldenrod;
        TxtConnFeedback.Foreground = System.Windows.Media.Brushes.White;
        BorderConnFeedback.Visibility = Visibility.Visible;

        var connStr = BuildConnectionString();
        try
        {
            using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            TxtConnFeedback.Text = $"✔ Connection Successful! Connected to \"{TxtDbName.Text.Trim()}\" on {TxtDbHost.Text.Trim()}:{TxtDbPort.Text.Trim()}.";
            BorderConnFeedback.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 64, 46));
            BorderConnFeedback.BorderBrush = System.Windows.Media.Brushes.LightGreen;
            TxtConnFeedback.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            var pex = ex as PostgresException ?? ex.InnerException as PostgresException;
            var isDbNotFound = (pex != null && pex.SqlState == "3D000") || ex.Message.Contains("3D000") || ex.Message.ToLower().Contains("does not exist");

            if (isDbNotFound)
            {
                try
                {
                    TxtConnFeedback.Text = $"Database \"{TxtDbName.Text.Trim()}\" does not exist yet. Creating database automatically on server...";
                    await EnsureDatabaseExistsAsync();

                    using var conn2 = new NpgsqlConnection(connStr);
                    await conn2.OpenAsync();

                    TxtConnFeedback.Text = $"✔ Database \"{TxtDbName.Text.Trim()}\" created and connected successfully!";
                    BorderConnFeedback.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 64, 46));
                    BorderConnFeedback.BorderBrush = System.Windows.Media.Brushes.LightGreen;
                    TxtConnFeedback.Foreground = System.Windows.Media.Brushes.LightGreen;
                    return;
                }
                catch (Exception createEx)
                {
                    TxtConnFeedback.Text = $"⚠ Database does not exist and automatic creation failed: {createEx.Message}";
                    BorderConnFeedback.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 30, 30));
                    BorderConnFeedback.BorderBrush = System.Windows.Media.Brushes.IndianRed;
                    TxtConnFeedback.Foreground = System.Windows.Media.Brushes.LightPink;
                    return;
                }
            }

            TxtConnFeedback.Text = $"❌ Connection Failed: {ex.Message}";
            BorderConnFeedback.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 30, 30));
            BorderConnFeedback.BorderBrush = System.Windows.Media.Brushes.IndianRed;
            TxtConnFeedback.Foreground = System.Windows.Media.Brushes.LightPink;
        }
    }

    private async void OnCompleteClick(object sender, RoutedEventArgs e)
    {
        TxtWizardError.Visibility = Visibility.Collapsed;

        var username = TxtOwnerUsername.Text.Trim();
        var fullName = TxtOwnerFullName.Text.Trim();
        var password = TxtOwnerPassword.Password;
        var confirm = TxtOwnerPasswordConfirm.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Owner username and password are required.");
            return;
        }

        if (password != confirm)
        {
            ShowError("Passwords do not match.");
            return;
        }

        var company = new CompanyProfileDto(
            TxtCompanyName.Text.Trim(),
            TxtRegNo.Text.Trim(),
            TxtAddress.Text.Trim(),
            TxtPhone.Text.Trim(),
            null,
            TxtGstNo.Text.Trim(),
            "2024-25"
        );

        try
        {
            // 1. Verify and create database if missing
            try
            {
                await EnsureDatabaseExistsAsync();
            }
            catch { }

            // 2. Persist database connection settings to durable ProgramData and local config
            var connStr = BuildConnectionString();
            var json = JsonSerializer.Serialize(new
            {
                ConnectionStrings = new
                {
                    DefaultConnection = connStr
                },
                Serilog = new
                {
                    MinimumLevel = "Information"
                }
            }, new JsonSerializerOptions { WriteIndented = true });

            try
            {
                var progDataDir = App.ProgramDataDirectory;
                Directory.CreateDirectory(progDataDir);
                await File.WriteAllTextAsync(Path.Combine(progDataDir, "appsettings.json"), json);
            }
            catch { }

            try
            {
                await File.WriteAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), json);
            }
            catch { }

            // 3. Complete First Run Account and Company Registration
            await _authService.CompleteFirstRunWizardAsync(username, password, fullName, company);
            MessageBox.Show("First-run setup completed successfully! Welcome to Jeba Gold Finance.", "JGF Setup Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Setup failed: {ex.Message}");
        }
    }

    private void ShowError(string msg)
    {
        TxtWizardError.Text = msg;
        TxtWizardError.Visibility = Visibility.Visible;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
