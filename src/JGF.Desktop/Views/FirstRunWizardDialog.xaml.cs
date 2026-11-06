using System.Windows;
using JGF.Application.Security;

namespace JGF.Desktop.Views;

public partial class FirstRunWizardDialog : Window
{
    private readonly IAuthService _authService;

    public FirstRunWizardDialog(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
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
            await _authService.CompleteFirstRunWizardAsync(username, password, fullName, company);
            MessageBox.Show("First-run setup completed successfully! You can now log in.", "JGF Setup", MessageBoxButton.OK, MessageBoxImage.Information);
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
