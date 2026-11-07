using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace JGF.Desktop.Views.Controls;

public partial class DbConnectionSettingsControl : UserControl
{
    public event EventHandler? ConnectionConfigured;

    public DbConnectionSettingsControl()
    {
        InitializeComponent();
        Loaded += OnControlLoaded;
    }

    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(configPath))
            {
                var text = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csElem) &&
                    csElem.TryGetProperty("DefaultConnection", out var defConn))
                {
                    var connStr = defConn.GetString() ?? "";
                    var builder = new NpgsqlConnectionStringBuilder(connStr);
                    if (!string.IsNullOrEmpty(builder.Host)) TxtHost.Text = builder.Host;
                    if (builder.Port > 0) TxtPort.Text = builder.Port.ToString();
                    if (!string.IsNullOrEmpty(builder.Database)) TxtDatabase.Text = builder.Database;
                    if (!string.IsNullOrEmpty(builder.Username)) TxtUsername.Text = builder.Username;
                    if (!string.IsNullOrEmpty(builder.Password)) TxtPassword.Password = builder.Password;
                }
            }
        }
        catch
        {
            // Keep default values
        }
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TxtHost == null || CmbMode == null) return;

        if (CmbMode.SelectedIndex == 0)
        {
            TxtHost.Text = "localhost";
        }
        else
        {
            TxtHost.Text = "192.168.1.100"; // Example LAN IP of PC1
        }
    }

    private string BuildConnectionString()
    {
        var ssl = (CmbSslMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Prefer";
        return $"Host={TxtHost.Text.Trim()};Port={TxtPort.Text.Trim()};Database={TxtDatabase.Text.Trim()};Username={TxtUsername.Text.Trim()};Password={TxtPassword.Password};SSL Mode={ssl};Timeout=5;Command Timeout=15;";
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        var targetDb = TxtDatabase.Text.Trim();
        var ssl = (CmbSslMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Prefer";
        var adminConnStr = $"Host={TxtHost.Text.Trim()};Port={TxtPort.Text.Trim()};Database=postgres;Username={TxtUsername.Text.Trim()};Password={TxtPassword.Password};SSL Mode={ssl}";

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
        var connStr = BuildConnectionString();
        try
        {
            using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            TxtTestResult.Text = $"Connection Successful! Connected to PostgreSQL database \"{TxtDatabase.Text}\".";
            BorderTestResult.Background = System.Windows.Media.Brushes.Honeydew;
            TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkGreen;
            BorderTestResult.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            var pex = ex as PostgresException ?? ex.InnerException as PostgresException;
            var isDbNotFound = (pex != null && pex.SqlState == "3D000") || ex.Message.Contains("3D000") || ex.Message.ToLower().Contains("does not exist");

            if (isDbNotFound)
            {
                try
                {
                    TxtTestResult.Text = $"Database \"{TxtDatabase.Text}\" does not exist yet. Creating database automatically...";
                    BorderTestResult.Background = System.Windows.Media.Brushes.LightYellow;
                    TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                    BorderTestResult.Visibility = Visibility.Visible;

                    await EnsureDatabaseExistsAsync();

                    // Re-test connection to the newly created database
                    using var conn = new NpgsqlConnection(connStr);
                    await conn.OpenAsync();

                    TxtTestResult.Text = $"Database \"{TxtDatabase.Text}\" created and connected successfully!";
                    BorderTestResult.Background = System.Windows.Media.Brushes.Honeydew;
                    TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkGreen;
                    BorderTestResult.Visibility = Visibility.Visible;
                    return;
                }
                catch (Exception createEx)
                {
                    TxtTestResult.Text = $"Database not found and automatic creation failed: {createEx.Message}";
                    BorderTestResult.Background = System.Windows.Media.Brushes.MistyRose;
                    TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkRed;
                    BorderTestResult.Visibility = Visibility.Visible;
                    return;
                }
            }

            TxtTestResult.Text = $"Connection Failed: {ex.Message}";
            BorderTestResult.Background = System.Windows.Media.Brushes.MistyRose;
            TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkRed;
            BorderTestResult.Visibility = Visibility.Visible;
        }
    }

    private async void OnSaveConnectionClick(object sender, RoutedEventArgs e)
    {
        var connStr = BuildConnectionString();

        try
        {
            // Auto ensure database exists before saving
            try
            {
                await EnsureDatabaseExistsAsync();
            }
            catch { }

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

            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            await File.WriteAllTextAsync(configPath, json);

            // Also save to root development directory if running from Debug folder
            try
            {
                var devAppsettings = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\appsettings.json"));
                if (File.Exists(devAppsettings))
                {
                    await File.WriteAllTextAsync(devAppsettings, json);
                }
            }
            catch { }

            TxtTestResult.Text = "Settings saved successfully!";
            BorderTestResult.Background = System.Windows.Media.Brushes.Honeydew;
            TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkGreen;
            BorderTestResult.Visibility = Visibility.Visible;

            var result = MessageBox.Show(
                "Database connection saved successfully!\n\nWould you like the application to restart now to connect to the database?",
                "Connection Saved",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                ConnectionConfigured?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
