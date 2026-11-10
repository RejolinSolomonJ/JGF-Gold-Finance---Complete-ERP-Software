using System;
using System.Collections.Generic;
using System.Windows;
using JGF.Application.Receipts;
using JGF.Desktop.Common;
using JGF.Domain.Entities;

namespace JGF.Desktop.Views;

public partial class PastReceiptsDialog : Window
{
    private readonly Loan _loan;
    private readonly Customer _customer;
    private readonly IReceiptService _receiptService;

    public PastReceiptsDialog(Loan loan, Customer customer, List<Receipt> receipts, IReceiptService receiptService)
    {
        InitializeComponent();
        _loan = loan;
        _customer = customer;
        _receiptService = receiptService;

        TxtHeaderTitle.Text = $"Receipts History for Loan #{_loan.LoanNumber}";
        TxtHeaderSubtitle.Text = $"{_customer.Name} ({_customer.Code}) • Mobile: {_customer.Phone} • {receipts.Count} Receipt(s) Found";

        GridReceipts.ItemsSource = receipts;
    }

    private void OnPrintRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Receipt receipt })
        {
            ReceiptPrintHelper.PrintReceipt(receipt, _loan, _customer, this, isDuplicate: true);
        }
    }

    private async void OnDownloadRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Receipt receipt })
        {
            await ReceiptPrintHelper.DownloadReceiptPdfAsync(_receiptService, receipt.Id, receipt.ReceiptNumber, this);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

