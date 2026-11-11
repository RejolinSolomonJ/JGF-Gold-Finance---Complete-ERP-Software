using System.Windows;
using System.Windows.Controls;
using JGF.Application.Loans;

namespace JGF.Desktop.Views.Controls;

public partial class LoanHistoryControl : UserControl
{
    private readonly ILoanService _loanService;

    public LoanHistoryControl(ILoanService loanService)
    {
        InitializeComponent();
        _loanService = loanService;
    }

    public async void OnLoadHistoryClick(object? sender = null, RoutedEventArgs? e = null)
    {
        await LoadHistoryAsync(TxtLoanNo.Text.Trim());
    }

    public async Task LoadHistoryAsync(string loanNumber)
    {
        if (string.IsNullOrWhiteSpace(loanNumber)) return;
        TxtLoanNo.Text = loanNumber;

        var loan = await _loanService.FindLoanByNumberOrBarcodeAsync(loanNumber);
        if (loan == null)
        {
            MessageBox.Show($"Loan '{loanNumber}' not found.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var history = await _loanService.GetLoanHistoryAsync(loan.Id);
        GridHistory.ItemsSource = history;
    }
}

