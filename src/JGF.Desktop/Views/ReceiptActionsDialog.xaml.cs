using System;
using System.Windows;
using JGF.Application.Receipts;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Serilog;

namespace JGF.Desktop.Views;

public partial class ReceiptActionsDialog : Window
{
    private readonly Receipt _receipt;
    private readonly Loan _loan;
    private readonly Customer _customer;
    private readonly IReceiptService _receiptService;

    public ReceiptActionsDialog(
        Receipt receipt,
        Loan loan,
        Customer customer,
        IReceiptService receiptService)
    {
        InitializeComponent();
        _receipt = receipt;
        _loan = loan;
        _customer = customer;
        _receiptService = receiptService;

        TxtSuccessSubtitle.Text = $"Receipt #{_receipt.ReceiptNumber} • Total: {ValidationHelper.FormatCurrency(_receipt.TotalAmount)}";

        if (_receipt.ReceiptType == ReceiptType.Closure)
        {
            TxtSuccessTitle.Text = $"Loan {_loan.LoanNumber} Fully Closed & Redeemed!";
            BtnDownloadSlip.Visibility = Visibility.Visible;
        }

        // Render preview
        var visual = ReceiptPrintHelper.CreateReceiptVisual(_receipt, _loan, _customer);
        PnlPreviewContainer.Content = visual;
    }

    private void OnDirectPrintClick(object sender, RoutedEventArgs e)
    {
        ReceiptPrintHelper.PrintReceipt(_receipt, _loan, _customer, this);
    }

    private async void OnDownloadPdfClick(object sender, RoutedEventArgs e)
    {
        await ReceiptPrintHelper.DownloadReceiptPdfAsync(_receiptService, _receipt.Id, _receipt.ReceiptNumber, this);
    }

    private async void OnDownloadSlipClick(object sender, RoutedEventArgs e)
    {
        await ReceiptPrintHelper.DownloadReleaseSlipPdfAsync(_receiptService, _loan.Id, _loan.LoanNumber, this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

