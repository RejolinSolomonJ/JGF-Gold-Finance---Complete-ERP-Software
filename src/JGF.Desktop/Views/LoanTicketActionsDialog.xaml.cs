using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Xps.Packaging;
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
    private XpsDocument? _xpsDoc;
    private string? _tempXpsPath;

    public LoanTicketActionsDialog(Loan loan, Customer customer, Scheme scheme)
    {
        InitializeComponent();
        _loan = loan ?? throw new ArgumentNullException(nameof(loan));
        _customer = customer ?? throw new ArgumentNullException(nameof(customer));
        _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));

        TxtSuccessTitle.Text = $"Jewel Loan #{_loan.LoanNumber} Disbursed Successfully!";
        TxtSuccessSubtitle.Text = $"Official JGF Stationery Document (with Gold Logo) Loaded Directly in App!";

        Loaded += (s, e) =>
        {
            RbSheet1.IsChecked = true;
            LoadVisuals();
        };

        Closed += (s, e) =>
        {
            try
            {
                _xpsDoc?.Close();
                _xpsDoc = null;
                if (!string.IsNullOrEmpty(_tempXpsPath) && File.Exists(_tempXpsPath))
                {
                    File.Delete(_tempXpsPath);
                }
            }
            catch { }
        };
    }

    private void LoadVisuals()
    {
        try
        {
            // 1. Render the exact Word/PDF vector document directly inside WPF using XPS
            var tempDir = Path.Combine(Path.GetTempPath(), "JGF_Tickets");
            Directory.CreateDirectory(tempDir);
            _tempXpsPath = Path.Combine(tempDir, $"JGF_{Guid.NewGuid():N}.xps");

            if (LoanWordDocumentHelper.ExportWordToXps(_loan, _customer, _scheme, _tempXpsPath))
            {
                _xpsDoc = new XpsDocument(_tempXpsPath, FileAccess.Read);
                DocViewer.Document = _xpsDoc.GetFixedDocumentSequence();
                DocViewer.Visibility = Visibility.Visible;
                if (FallbackScrollViewer != null) FallbackScrollViewer.Visibility = Visibility.Collapsed;
                DocViewer.FitToWidth();
                return;
            }
        }
        catch (Exception xpsEx)
        {
            Log.Warning(xpsEx, "XPS native document rendering fell back to XAML visual.");
        }

        try
        {
            // 2. Fallback to native WPF XAML visuals if Word is not active
            _visualSheet1 = LoanPawnTicketPrintHelper.CreateMortgageDeedVisual(_loan, _customer, _scheme, _loan.Receipts?.ToList());
            _visualSheet2 = LoanPawnTicketPrintHelper.CreateInventoryAndSlipVisual(_loan, _customer, _scheme);

            DocViewer.Visibility = Visibility.Collapsed;
            if (FallbackScrollViewer != null) FallbackScrollViewer.Visibility = Visibility.Visible;
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
        if (DocViewer != null && DocViewer.Visibility == Visibility.Visible)
        {
            if (RbSheet1.IsChecked == true)
            {
                DocViewer.GoToPage(1);
            }
            else
            {
                DocViewer.GoToPage(2);
            }
            return;
        }

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
        // Direct print via Word if available
        if (LoanWordDocumentHelper.PrintWordDocumentDirectly(_loan, _customer, _scheme))
        {
            MessageBox.Show("Exact 2-page document sent directly to printer!", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

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

    private void OnOpenWordClick(object sender, RoutedEventArgs e)
    {
        LoanWordDocumentHelper.OpenInWord(_loan, _customer, _scheme);
    }

    private void OnSaveWordClick(object sender, RoutedEventArgs e)
    {
        LoanWordDocumentHelper.SaveWordDocument(_loan, _customer, _scheme, this);
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
