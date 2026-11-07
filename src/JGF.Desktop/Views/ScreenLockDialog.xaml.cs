using System.Windows;
using System.Windows.Input;
using JGF.Application.Security;
using JGF.Domain.Entities;

namespace JGF.Desktop.Views;

public partial class ScreenLockDialog : Window
{
    private readonly User _user;
    private readonly IPasswordHasher _passwordHasher;
    public bool ShouldLogout { get; private set; }

    public ScreenLockDialog(User user, IPasswordHasher passwordHasher)
    {
        InitializeComponent();
        _user = user;
        _passwordHasher = passwordHasher;

        TxtLockedUser.Text = $"{user.FullName} ({user.Role})";
        TxtLockedUsername.Text = $"Username: {user.Username}";

        Loaded += (s, e) => TxtPassword.Focus();
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AttemptUnlock();
        }
    }

    private void OnUnlockClick(object sender, RoutedEventArgs e)
    {
        AttemptUnlock();
    }

    private void AttemptUnlock()
    {
        var password = TxtPassword.Password;
        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your password.");
            return;
        }

        if (_passwordHasher.VerifyPassword(password, _user.PasswordHash))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError("Incorrect password. Please try again.");
            TxtPassword.SelectAll();
            TxtPassword.Focus();
        }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }

    private void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        ShouldLogout = true;
        DialogResult = false;
        Close();
    }
}
