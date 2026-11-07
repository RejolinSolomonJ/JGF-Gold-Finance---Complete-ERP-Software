using System.Windows;
using JGF.Application.Security;
using JGF.Domain.Entities;

namespace JGF.Desktop.Views;

public partial class LoginDialog : Window
{
    private readonly IAuthService _authService;
    public User? AuthenticatedUser { get; private set; }

    public LoginDialog(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        Loaded += (s, e) =>
        {
            if (string.IsNullOrEmpty(TxtUsername.Text))
            {
                TxtUsername.Text = "admin";
                TxtPassword.Password = "admin";
            }
            TxtUsername.Focus();
            TxtUsername.SelectAll();
        };
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        var username = TxtUsername.Text.Trim();
        var password = TxtPassword.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Please enter both username and password.");
            return;
        }

        var result = await _authService.LoginAsync(username, password);
        if (result.Succeeded && result.User != null)
        {
            AuthenticatedUser = result.User;
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError(result.ErrorMessage ?? "Login failed.");
        }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

