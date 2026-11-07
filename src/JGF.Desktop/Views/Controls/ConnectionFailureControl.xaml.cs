using System.Windows;
using System.Windows.Controls;

namespace JGF.Desktop.Views.Controls;

public partial class ConnectionFailureControl : UserControl
{
    public event EventHandler? RetryRequested;
    public event EventHandler? OpenSettingsRequested;

    public ConnectionFailureControl(string host, int port, string errorDetails)
    {
        InitializeComponent();
        TxtEndpoint.Text = $"Host: {host} | Port: {port}";
        TxtErrorDetails.Text = errorDetails;
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
