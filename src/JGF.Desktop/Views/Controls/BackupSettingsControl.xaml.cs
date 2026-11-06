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
        Loaded += async (s, e) => await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        var logs = await _backupService.GetBackupLogsAsync(30);
        GridLogs.ItemsSource = logs;
    }

    private async void OnRunBackupClick(object sender, RoutedEventArgs e)
    {
        var key = TxtKey.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Please enter an encryption passphrase.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = await _backupService.CreateBackupAsync(key);
        if (result.Success)
        {
            MessageBox.Show($"Encrypted backup completed successfully!\nFile: {result.FilePath}\nSize: {result.FileSize / 1024} KB", "Backup Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"Backup failed: {result.ErrorMessage}", "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            MessageBox.Show("Select a backup file to restore.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var key = TxtKey.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Enter the encryption passphrase.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var success = await _backupService.RestoreBackupAsync(TxtRestoreFile.Text, key, "jgf_test_db");
        if (success)
        {
            MessageBox.Show("Restored successfully into test database 'jgf_test_db'!", "Test Restore Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Test restore failed. Check database logs.", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

