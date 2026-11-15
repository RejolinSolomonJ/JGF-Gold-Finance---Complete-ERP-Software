using System;
using System.IO;
using System.Linq;
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
        TxtSuccessSubtitle.Text = $"Amount: ₹ {_loan.LoanAmount:N2} | Net Wt: {_loan.NetWeight:F3} g | Customer: {_customer.Name} ({_customer.Code})";

        Loaded += (s, e) =>
        {
            LoadVisuals();
            RbSheet1.IsChecked = true;
            UpdateActivePreview();
        };
    }

    private void LoadVisuals()
    {
        try
        {
            _visualSheet1 = LoanPawnTicketPrintHelper.CreateMortgageDeedVisual(_loan, _customer, _scheme, _loan.Receipts?.ToList());
            _visualSheet2 = LoanPawnTicketPrintHelper.CreateInventoryAndSlipVisual(_loan, _customer, _scheme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to build loan ticket visuals.");
            MessageBox.Show($"Failed to generate document preview: {ex.Message}", "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateActivePreview()
    {
        if (PnlPreviewContainer == null || RbSheet1 == null) return;

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
            ? LoanPawnTicketPrintHelper.CreateMortgageDeedVisual(_loan, _customer, _scheme, _loan.Receipts?.ToList())
            : LoanPawnTicketPrintHelper.CreateInventoryAndSlipVisual(_loan, _customer, _scheme);

        var title = isSheet1
            ? $"JGF Loan {_loan.LoanNumber} - Mortgage Deed & Ledger"
            : $"JGF Loan {_loan.LoanNumber} - Jewel Inventory & Customer Slip";

        LoanPawnTicketPrintHelper.PrintTicket(visual, title, this);
    }

    private void OnPrintBothClick(object sender, RoutedEventArgs e)
    {
        var v1 = LoanPawnTicketPrintHelper.CreateMortgageDeedVisual(_loan, _customer, _scheme, _loan.Receipts?.ToList());
        var v2 = LoanPawnTicketPrintHelper.CreateInventoryAndSlipVisual(_loan, _customer, _scheme);

        // Print Sheet 1 (Deed)
        var p1Success = LoanPawnTicketPrintHelper.PrintTicket(v1, $"JGF Loan {_loan.LoanNumber} - Sheet 1 (Mortgage Deed)", this);
        if (p1Success)
        {
            var res = MessageBox.Show(
                "Sheet 1 (Mortgage Deed) sent to printer.\n\nReady to print Sheet 2 (Jewel Inventory & Customer Slip)?",
                "Print Next Sheet",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                LoanPawnTicketPrintHelper.PrintTicket(v2, $"JGF Loan {_loan.LoanNumber} - Sheet 2 (Inventory & Customer Slip)", this);
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
