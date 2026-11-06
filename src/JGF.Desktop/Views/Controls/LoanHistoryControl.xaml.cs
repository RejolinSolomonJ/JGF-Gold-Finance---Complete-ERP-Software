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

    private async void OnLoadHistoryClick(object sender, RoutedEventArgs e)
    {
        var q = TxtLoanNo.Text.Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        var loan = await _loanService.FindLoanByNumberOrBarcodeAsync(q);
        if (loan == null)
        {
            MessageBox.Show($"Loan '{q}' not found.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var history = await _loanService.GetLoanHistoryAsync(loan.Id);
        GridHistory.ItemsSource = history;
    }
}
