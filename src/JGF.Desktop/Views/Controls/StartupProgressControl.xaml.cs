using System.Windows.Controls;

namespace JGF.Desktop.Views.Controls;

public partial class StartupProgressControl : UserControl
{
    public StartupProgressControl(string message = "Connecting to PostgreSQL database...")
    {
        InitializeComponent();
        TxtStatusMessage.Text = message;
    }

    public void UpdateStatus(string message)
    {
        TxtStatusMessage.Text = message;
    }
}
