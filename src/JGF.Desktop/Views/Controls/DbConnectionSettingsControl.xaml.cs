using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace JGF.Desktop.Views.Controls;

public partial class DbConnectionSettingsControl : UserControl
{
    public DbConnectionSettingsControl()
    {
        InitializeComponent();
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
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
        return $"Host={TxtHost.Text.Trim()};Port={TxtPort.Text.Trim()};Database={TxtDatabase.Text.Trim()};Username={TxtUsername.Text.Trim()};Password={TxtPassword.Password};SSL Mode={ssl}";
    }

    private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
    {
        var connStr = BuildConnectionString();
        try
        {
            using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            TxtTestResult.Text = $"Connection Successful! Connected to PostgreSQL server on {TxtHost.Text}.";
            BorderTestResult.Background = System.Windows.Media.Brushes.Honeydew;
            TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkGreen;
            BorderTestResult.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            TxtTestResult.Text = $"Connection Failed: {ex.Message}";
            BorderTestResult.Background = System.Windows.Media.Brushes.MistyRose;
            TxtTestResult.Foreground = System.Windows.Media.Brushes.DarkRed;
            BorderTestResult.Visibility = Visibility.Visible;
        }
    }

    private async void OnSaveConnectionClick(object sender, RoutedEventArgs e)
    {
        var connStr = BuildConnectionString();
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        try
        {
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

            await File.WriteAllTextAsync(configPath, json);
            MessageBox.Show("Database connection saved to appsettings.json! Please restart the app for changes to take effect.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
