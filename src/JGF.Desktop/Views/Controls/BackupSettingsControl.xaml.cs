using System.IO;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;
using Microsoft.Win32;

namespace JGF.Desktop.Views.Controls;

public partial class BackupSettingsControl : UserControl
{
    private readonly IBackupService _backupService;

    public BackupSettingsControl(IBackupService backupService)
    {
        InitializeComponent();
        _backupService = backupService;
        Loaded += async (s, e) =>
        {
            await LoadSettingsAsync();
            await LoadLogsAsync();
        };
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _backupService.GetBackupSettingsAsync();
            TxtCloudFolder.Text = settings.CloudSyncPath;
            TxtLanFolder.Text = settings.LanSharePath;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load backup settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            var logs = await _backupService.GetBackupLogsAsync(50);
            GridLogs.ItemsSource = logs;
        }
        catch
        {
            // Best effort
        }
    }

    private void OnBrowseCloudFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Cloud Sync Folder (OneDrive / Google Drive)",
            InitialDirectory = Directory.Exists(TxtCloudFolder.Text) ? TxtCloudFolder.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            TxtCloudFolder.Text = dialog.FolderName;
        }
    }

    private void OnBrowseLanFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select PC2 LAN Shared Destination Folder",
            InitialDirectory = Directory.Exists(TxtLanFolder.Text) ? TxtLanFolder.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            TxtLanFolder.Text = dialog.FolderName;
        }
    }

    private async void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCloudFolder.Text))
        {
            MessageBox.Show("Please specify a valid Cloud Sync folder path.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var settings = new BackupSettingsDto(TxtCloudFolder.Text.Trim(), TxtLanFolder.Text.Trim());
            await _backupService.SaveBackupSettingsAsync(settings);
            MessageBox.Show("Backup configuration successfully saved!\n\n• Cloud Folder: Lifetime Archive (Never auto-deleted)\n• Local Folder: 30-Day Rolling Vault", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnRunBackupClick(object sender, RoutedEventArgs e)
    {
        var key = TxtKey.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Please enter an encryption passphrase to encrypt your database snapshot.", "Passphrase Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Auto-save current folder settings before running
        if (!string.IsNullOrWhiteSpace(TxtCloudFolder.Text))
        {
            try
            {
                var settings = new BackupSettingsDto(TxtCloudFolder.Text.Trim(), TxtLanFolder.Text.Trim());
                await _backupService.SaveBackupSettingsAsync(settings);
            }
            catch { }
        }

        var result = await _backupService.CreateBackupAsync(key);
        if (result.Success)
        {
            var cloudMsg = !string.IsNullOrEmpty(result.CloudPath)
                ? $"\n\n☁️ Lifetime Cloud Sync:\n{result.CloudPath}"
                : "";

            MessageBox.Show(
                $"Encrypted database backup completed successfully!\n\n📁 Local Vault:\n{result.FilePath}\n\n📦 Size: {result.FileSize / 1024:N0} KB{cloudMsg}\n\nNotice: Cloud backups are preserved permanently for lifetime history.",
                "Backup Succeeded",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"Backup failed: {result.ErrorMessage}", "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        await LoadLogsAsync();
    }

    private async void OnRefreshLogsClick(object sender, RoutedEventArgs e)
    {
        await LoadLogsAsync();
    }

    private void OnBrowseRestoreClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Encrypted Backup File",
            Filter = "Encrypted Backup Files (*.enc)|*.enc|All Files (*.*)|*.*"
        };
        if (ofd.ShowDialog() == true)
        {
            TxtRestoreFile.Text = ofd.FileName;
        }
    }

    private async void OnTestRestoreClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtRestoreFile.Text))
        {
            MessageBox.Show("Select an encrypted backup file to restore.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var key = TxtKey.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Enter the encryption passphrase used when creating this backup.", "Passphrase Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var success = await _backupService.RestoreBackupAsync(TxtRestoreFile.Text, key, "jgf_test_db");
        if (success)
        {
            MessageBox.Show("Restored successfully into test database 'jgf_test_db'!\nLive database remains untouched.", "Test Restore Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Test restore failed. Check database logs.", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

