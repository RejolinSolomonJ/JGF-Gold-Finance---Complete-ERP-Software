using System;
using System.Threading.Tasks;
using System.Windows;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using Serilog;

namespace JGF.Desktop.Views;

public partial class LoanTicketActionsDialog : Window
{
    private readonly Loan _loan;
    private readonly Customer _customer;
    private readonly Scheme _scheme;

    private FrameworkElement? _visualSheet1;
    private FrameworkElement? _visualSheet2;

    public LoanTicketActionsDialog(Loan loan, Customer customer, Scheme scheme)
    {
        InitializeComponent();
        _loan = loan ?? throw new ArgumentNullException(nameof(loan));
        _customer = customer ?? throw new ArgumentNullException(nameof(customer));
        _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));

        TxtSuccessTitle.Text = $"Jewel Loan #{_loan.LoanNumber} Disbursed Successfully!";
        TxtSuccessSubtitle.Text = $"Disbursed: ₹ {_loan.LoanAmount:N2} | Net Weight: {_loan.NetWeight:F3} g | Customer: {_customer.Name} ({_customer.Code})";

        Loaded += (s, e) => LoadVisuals();
    }

    private void LoadVisuals()
    {
        try
        {
            _visualSheet1 = LoanPawnTicketPrintHelper.CreateInventoryAndSlipVisual(_loan, _customer, _scheme);
            _visualSheet2 = LoanPawnTicketPrintHelper.CreateMortgageDeedVisual(_loan, _customer, _scheme, _loan.Receipts?.ToList());

            UpdateActivePreview();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to build loan ticket visuals.");
            MessageBox.Show($"Failed to generate document preview: {ex.Message}", "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateActivePreview()
    {
        if (RbSheet1.IsChecked == true)
        {
            PnlPreviewContainer.Content = _visualSheet1;
        }
        else
        {
            PnlPreviewContainer.Content = _visualSheet2;
        }
    }

    private void OnSheetSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateActivePreview();
    }

    private void OnPrintCurrentClick(object sender, RoutedEventArgs e)
    {
        var isSheet1 = RbSheet1.IsChecked == true;
        var visual = isSheet1
            ? LoanPawnTicketPrintHelper.CreateInventoryAndSlipVisual(_loan, _customer, _scheme)
            : LoanPawnTicketPrintHelper.CreateMortgageDeedVisual(_loan, _customer, _scheme, _loan.Receipts?.ToList());

        var title = isSheet1
            ? $"JGF Loan {_loan.LoanNumber} - Jewel Inventory & Customer Slip"
            : $"JGF Loan {_loan.LoanNumber} - Mortgage Deed & Ledger";

        LoanPawnTicketPrintHelper.PrintTicket(visual, title, this);
    }

    private void OnPrintBothClick(object sender, RoutedEventArgs e)
    {
        var v1 = LoanPawnTicketPrintHelper.CreateInventoryAndSlipVisual(_loan, _customer, _scheme);
        var v2 = LoanPawnTicketPrintHelper.CreateMortgageDeedVisual(_loan, _customer, _scheme, _loan.Receipts?.ToList());

        // Print Sheet 1
        var p1Success = LoanPawnTicketPrintHelper.PrintTicket(v1, $"JGF Loan {_loan.LoanNumber} - Sheet 1 (Inventory & Slip)", this);
        if (p1Success)
        {
            var res = MessageBox.Show(
                "Sheet 1 sent to printer.\n\nReady to print Sheet 2 (Legal Mortgage Deed)?",
                "Print Next Sheet",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                LoanPawnTicketPrintHelper.PrintTicket(v2, $"JGF Loan {_loan.LoanNumber} - Sheet 2 (Mortgage Deed & Ledger)", this);
            }
        }
    }

    private async void OnDownloadPdfClick(object sender, RoutedEventArgs e)
    {
        BtnDownloadPdf.IsEnabled = false;
        try
        {
            await LoanPawnTicketPrintHelper.DownloadTicketPdfAsync(_loan, _customer, _scheme, this);
        }
        finally
        {
            BtnDownloadPdf.IsEnabled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
