using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using JGF.Application.Updates;
using JGF.Domain.Common;

namespace JGF.Desktop.Views;

public partial class AboutDialog : Window
{
    private readonly ISafeUpdateService _updateService;
    private readonly IServiceProvider _services;
    private VersionStatusDto? _currentStatus;

    public AboutDialog(ISafeUpdateService updateService, IServiceProvider services)
    {
        InitializeComponent();
        _updateService = updateService;
        _services = services;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _currentStatus = await _updateService.CheckVersionStatusAsync();
            TxtAppVersion.Text = $"App Version: {_currentStatus.CurrentAppVersion.Raw}";
            TxtDbVersion.Text = $"Database: {_currentStatus.DatabaseVersion.Raw}";

            if (_currentStatus.Status == VersionComparisonResult.AppNewerThanDb)
            {
                BorderVersionStatus.Background = System.Windows.Media.Brushes.LightYellow;
                BorderVersionStatus.BorderBrush = System.Windows.Media.Brushes.DarkGoldenrod;
                TxtStatusHeadline.Text = "● Database Update Required";
                TxtStatusHeadline.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                TxtStatusDetails.Text = _currentStatus.Message;
                BtnOpenUpdateWizard.Visibility = Visibility.Visible;
            }
            else if (_currentStatus.Status == VersionComparisonResult.AppOlderThanDb)
            {
                BorderVersionStatus.Background = System.Windows.Media.Brushes.MistyRose;
                BorderVersionStatus.BorderBrush = System.Windows.Media.Brushes.Red;
                TxtStatusHeadline.Text = "● Application Update Required";
                TxtStatusHeadline.Foreground = System.Windows.Media.Brushes.DarkRed;
                TxtStatusDetails.Text = _currentStatus.Message;
            }
            else
            {
                BorderVersionStatus.Background = System.Windows.Media.Brushes.Honeydew;
                BorderVersionStatus.BorderBrush = System.Windows.Media.Brushes.DarkGreen;
                TxtStatusHeadline.Text = "● System Status: Fully Up to Date";
                TxtStatusHeadline.Foreground = System.Windows.Media.Brushes.DarkGreen;
                TxtStatusDetails.Text = $"App binary and database schema are synchronized on version {_currentStatus.CurrentAppVersion.Raw}.";
            }

            var history = await _updateService.GetUpdateHistoryAsync();
            GridHistory.ItemsSource = history;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load version history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenUpdateWizardClick(object sender, RoutedEventArgs e)
    {
        var wizard = new SafeUpdateWizardDialog(_updateService, _services);
        wizard.Owner = this;
        wizard.ShowDialog();
        OnLoaded(this, new RoutedEventArgs());
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnLinInfotechLinkClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.linsinfotech.in") { UseShellExecute = true });
        }
        catch { }
    }
}

