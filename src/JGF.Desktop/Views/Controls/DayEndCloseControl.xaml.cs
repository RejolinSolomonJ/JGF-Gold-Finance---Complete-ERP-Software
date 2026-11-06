using System.Windows;
using System.Windows.Controls;
using JGF.Application.Accounting;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public partial class DayEndCloseControl : UserControl
{
    private readonly IDayCloseService _dayCloseService;
    private DayCloseStatusDto? _currentStatus;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public DayEndCloseControl(IDayCloseService dayCloseService)
    {
        InitializeComponent();
        _dayCloseService = dayCloseService;

        Loaded += async (s, e) => await LoadDayStatusAsync();
    }

    private async Task LoadDayStatusAsync()
    {
        _currentStatus = await _dayCloseService.GetDayStatusAsync(_today);

        TxtDayCloseDate.Text = $"Date: {_currentStatus.Date:dd-MMM-yyyy}";
        TxtOpening.Text = $"₹ {_currentStatus.OpeningCash:N2}";
        TxtReceipts.Text = $"₹ {_currentStatus.TotalCashIn:N2}";
        TxtPayments.Text = $"₹ {_currentStatus.TotalCashOut:N2}";
        TxtActiveCount.Text = $"{_currentStatus.ActiveLoansCount} active loans";
        TxtPortfolioPrincipal.Text = $"₹ {_currentStatus.TotalOutstandingPrincipal:N2}";
        TxtCalculatedClosing.Text = $"₹ {_currentStatus.ClosingCash:N2}";

        TxtCountedCash.Text = _currentStatus.ClosingCash.ToString("F2");
        UpdateVariance();

        if (_currentStatus.Status == DayCloseStatus.Closed)
        {
            TxtStatusBadge.Text = "[DAY CLOSED]";
            TxtStatusBadge.Foreground = System.Windows.Media.Brushes.Crimson;
            BorderDayState.Background = System.Windows.Media.Brushes.MistyRose;
            BorderDayState.BorderBrush = System.Windows.Media.Brushes.Crimson;
            TxtClosedByInfo.Text = $"Closed at {_currentStatus.ClosedAtUtc:dd-MMM-yyyy HH:mm} UTC by {_currentStatus.ClosedByUsername}. Edits locked.";
            BtnCloseDay.IsEnabled = false;
            BtnReopenDay.Visibility = Visibility.Visible;
        }
        else
        {
            TxtStatusBadge.Text = "[DAY OPEN]";
            TxtStatusBadge.Foreground = System.Windows.Media.Brushes.DarkGreen;
            BorderDayState.Background = System.Windows.Media.Brushes.Honeydew;
            BorderDayState.BorderBrush = System.Windows.Media.Brushes.DarkGreen;
            TxtClosedByInfo.Text = "Entries permitted for Cashier and Manager.";
            BtnCloseDay.IsEnabled = true;
            BtnReopenDay.Visibility = Visibility.Collapsed;
        }
    }

    private void OnCountedCashChanged(object sender, TextChangedEventArgs e)
    {
        UpdateVariance();
    }

    private void UpdateVariance()
    {
        if (_currentStatus == null) return;
        decimal.TryParse(TxtCountedCash.Text, out var counted);
        var diff = counted - _currentStatus.ClosingCash;

        if (diff == 0)
        {
            TxtCashVariance.Text = "₹ 0.00 (Matched ✓)";
            TxtCashVariance.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }
        else
        {
            TxtCashVariance.Text = $"Variance: ₹ {diff:N2} (Discrepancy ⚠)";
            TxtCashVariance.Foreground = System.Windows.Media.Brushes.Crimson;
        }
    }

    private async void OnExecuteCloseClick(object sender, RoutedEventArgs e)
    {
        decimal.TryParse(TxtCountedCash.Text, out var counted);

        var confirm = MessageBox.Show(
            $"Are you sure you want to CLOSE the day for {_today:dd-MMM-yyyy}?\nCounted Cash: ₹{counted:N2}\n\nOnce closed, transaction entries will be locked.",
            "Confirm Day-End Close",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _dayCloseService.ExecuteDayEndCloseAsync(
                _today,
                counted,
                TxtRemarks.Text.Trim(),
                Guid.Empty,
                UserRole.Owner
            );

            MessageBox.Show($"Day {_today:dd-MMM-yyyy} closed successfully!\nDaily encrypted backup triggered.", "Day Closed", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadDayStatusAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Day-End close failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnReopenDayClick(object sender, RoutedEventArgs e)
    {
        var reason = Microsoft.VisualBasic.Interaction.InputBox("Enter reason for reopening closed day (Owner Audit):", "Reopen Day Reason", "Rectifying ledger adjustment");
        if (string.IsNullOrWhiteSpace(reason)) return;

        try
        {
            await _dayCloseService.ReopenDayAsync(_today, reason, Guid.Empty, UserRole.Owner);
            MessageBox.Show("Day reopened. Entries are now permitted.", "Reopened", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadDayStatusAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Reopen failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

