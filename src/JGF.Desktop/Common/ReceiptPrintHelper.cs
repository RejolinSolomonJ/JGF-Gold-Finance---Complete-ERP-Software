using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using JGF.Application.Receipts;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Win32;
using Serilog;

namespace JGF.Desktop.Common;

public static class ReceiptPrintHelper
{
    private static readonly FontFamily DefaultFont = new("Segoe UI, Noto Sans Tamil, Nirmala UI, Arial");
    private static readonly SolidColorBrush PrimaryBlue = new(Color.FromRgb(0x1E, 0x3A, 0x8A));
    private static readonly SolidColorBrush GoldBrush = new(Color.FromRgb(0xB8, 0x86, 0x0B));
    private static readonly SolidColorBrush TextDark = new(Color.FromRgb(0x0F, 0x17, 0x2A));
    private static readonly SolidColorBrush BorderBrush = new(Color.FromRgb(0xCB, 0xD5, 0xE1));
    private static readonly SolidColorBrush CardBg = new(Color.FromRgb(0xF8, 0xFA, 0xFC));
    private static readonly SolidColorBrush GreenBg = new(Color.FromRgb(0xDC, 0xFC, 0xE7));
    private static readonly SolidColorBrush GreenText = new(Color.FromRgb(0x16, 0x65, 0x34));

    /// <summary>
    /// Builds a print-ready WPF Visual for a Payment Receipt.
    /// </summary>
    public static FrameworkElement CreateReceiptVisual(Receipt receipt, Loan loan, Customer customer, bool isDuplicate = false)
    {
        var container = new Border
        {
            Width = 460,
            Background = Brushes.White,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20)
        };
        TextElement.SetFontFamily(container, DefaultFont);

        var stack = new StackPanel();
        container.Child = stack;

        // Header
        var headerBorder = new Border
        {
            Background = CardBg,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var headerStack = new StackPanel();
        headerStack.Children.Add(new TextBlock
        {
            Text = "JEBA GOLD FINANCE (JGF)",
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            Foreground = PrimaryBlue,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = "நகை கடன் நிதி மென்பொருள்",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = GoldBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = "Main Road, Chennai | Helpline: 9876543210 | Reg: JGF-TN-2024",
            FontSize = 10,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });
        headerBorder.Child = headerStack;
        stack.Children.Add(headerBorder);

        // Receipt Title
        var titleText = isDuplicate ? "PAYMENT RECEIPT (DUPLICATE / நகல்)" : "JEWEL LOAN PAYMENT RECEIPT (ரசீது)";
        stack.Children.Add(new TextBlock
        {
            Text = titleText,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = PrimaryBlue,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        // Separator
        stack.Children.Add(new Border
        {
            Height = 2,
            Background = GoldBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // 2-Column Details Grid
        var gridMeta = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        gridMeta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridMeta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 4; i++) gridMeta.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddMeta(int r, int c, string label, string val, bool isGold = false)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 6, 2) };
            p.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, FontSize = 11, Width = 95, Foreground = TextDark });
            p.Children.Add(new TextBlock { Text = val, FontWeight = FontWeights.Normal, FontSize = 11, Foreground = isGold ? GoldBrush : TextDark });
            Grid.SetRow(p, r);
            Grid.SetColumn(p, c);
            gridMeta.Children.Add(p);
        }

        AddMeta(0, 0, "Receipt No:", receipt.ReceiptNumber);
        AddMeta(0, 1, "Loan No:", loan.LoanNumber, isGold: true);
        AddMeta(1, 0, "Date:", receipt.ReceiptDate.ToString("dd-MMM-yyyy"));
        AddMeta(1, 1, "Customer:", $"{customer.Name} ({customer.Code})");
        AddMeta(2, 0, "Pay Mode:", receipt.PaymentMode.ToString());
        AddMeta(2, 1, "Mobile:", customer.Phone);
        AddMeta(3, 0, "Receipt Type:", receipt.ReceiptType.ToString());
        AddMeta(3, 1, "Interest To:", $"{receipt.InterestCalculatedToDate:dd-MMM-yyyy} ({receipt.DaysCharged} days)");

        stack.Children.Add(gridMeta);

        // Particulars Table Header
        var thBorder = new Border
        {
            Background = PrimaryBlue,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 0)
        };
        var thDock = new DockPanel();
        thDock.Children.Add(new TextBlock { Text = "PARTICULARS / விவரம்", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11 });
        var thAmt = new TextBlock { Text = "AMOUNT (₹)", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11 };
        DockPanel.SetDock(thAmt, Dock.Right);
        thDock.Children.Add(thAmt);
        thBorder.Child = thDock;
        stack.Children.Add(thBorder);

        // Items
        void AddRow(string label, decimal amount)
        {
            var rBdr = new Border
            {
                Background = CardBg,
                BorderBrush = BorderBrush,
                BorderThickness = new Thickness(1, 0, 1, 1),
                Padding = new Thickness(8, 5, 8, 5)
            };
            var d = new DockPanel();
            d.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = TextDark });
            var a = new TextBlock { Text = $"₹ {amount:N2}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = TextDark };
            DockPanel.SetDock(a, Dock.Right);
            d.Children.Add(a);
            rBdr.Child = d;
            stack.Children.Add(rBdr);
        }

        AddRow("Principal Amount Paid", receipt.PrincipalAmount);
        AddRow("Accrued Interest Paid", receipt.InterestAmount);
        if (receipt.PenalInterestAmount > 0) AddRow("Penal Interest", receipt.PenalInterestAmount);
        if (receipt.PostalCharges > 0) AddRow("Postal / Notice Charges", receipt.PostalCharges);
        if (receipt.OtherCharges > 0) AddRow("Other Charges", receipt.OtherCharges);

        // Total Received Box
        var totalBdr = new Border
        {
            Background = GreenBg,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 8, 0, 12)
        };
        var totalDock = new DockPanel();
        totalDock.Children.Add(new TextBlock { Text = "NET TOTAL RECEIVED:", FontWeight = FontWeights.Bold, FontSize = 13, Foreground = GreenText, VerticalAlignment = VerticalAlignment.Center });
        var totalAmt = new TextBlock { Text = $"₹ {receipt.TotalAmount:N2}", FontWeight = FontWeights.Bold, FontSize = 16, Foreground = GreenText };
        DockPanel.SetDock(totalAmt, Dock.Right);
        totalDock.Children.Add(totalAmt);
        totalBdr.Child = totalDock;
        stack.Children.Add(totalBdr);

        // Terms
        var termsBdr = new Border
        {
            Background = CardBg,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var termsStack = new StackPanel();
        termsStack.Children.Add(new TextBlock
        {
            Text = "• Pledged gold ornaments remain securely insured in Bank/Vault lockers.",
            FontSize = 9,
            Foreground = Brushes.Gray
        });
        termsStack.Children.Add(new TextBlock
        {
            Text = "• Ornaments will be released only upon full settlement & presentation of original ticket.",
            FontSize = 9,
            Foreground = Brushes.Gray
        });
        termsStack.Children.Add(new TextBlock
        {
            Text = "• Computer generated receipt. Retain for all future transactions.",
            FontSize = 9,
            Foreground = Brushes.Gray
        });
        termsBdr.Child = termsStack;
        stack.Children.Add(termsBdr);

        // Signatures Grid
        var sigGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sigLeft = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        sigLeft.Children.Add(new Border { Width = 140, Height = 1, Background = Brushes.Black, Margin = new Thickness(0, 0, 0, 4) });
        sigLeft.Children.Add(new TextBlock { Text = "Authorized Cashier / JGF", FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
        Grid.SetColumn(sigLeft, 0);
        sigGrid.Children.Add(sigLeft);

        var sigRight = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        sigRight.Children.Add(new Border { Width = 140, Height = 1, Background = Brushes.Black, Margin = new Thickness(0, 0, 0, 4) });
        sigRight.Children.Add(new TextBlock { Text = "Customer / Payer Signature", FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
        Grid.SetColumn(sigRight, 1);
        sigGrid.Children.Add(sigRight);

        stack.Children.Add(sigGrid);

        return container;
    }

    /// <summary>
    /// Sends the receipt visual directly to the printer using Windows PrintDialog.
    /// </summary>
    public static bool PrintReceipt(Receipt receipt, Loan loan, Customer customer, Window? owner = null, bool isDuplicate = false)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (owner != null)
            {
                // Align print dialog with owner
            }

            if (printDlg.ShowDialog() == true)
            {
                var visual = CreateReceiptVisual(receipt, loan, customer, isDuplicate);
                visual.Measure(new Size(printDlg.PrintableAreaWidth, double.PositiveInfinity));
                visual.Arrange(new Rect(new Point(0, 0), visual.DesiredSize));
                visual.UpdateLayout();

                printDlg.PrintVisual(visual, $"JGF Receipt {receipt.ReceiptNumber}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print receipt visual.");
            MessageBox.Show($"Direct print failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return false;
    }

    /// <summary>
    /// Generates the official PDF receipt and prompts user to save / download.
    /// </summary>
    public static async Task<bool> DownloadReceiptPdfAsync(IReceiptService receiptService, Guid receiptId, string receiptNumber, Window? owner = null)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Title = "Download Payment Receipt PDF",
                Filter = "PDF Document (*.pdf)|*.pdf|All Files (*.*)|*.*",
                FileName = $"JGF_Receipt_{receiptNumber.Replace('/', '_')}_{DateTime.Today:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog(owner) == true)
            {
                var pdfBytes = await receiptService.GenerateReceiptPdfAsync(receiptId);
                await File.WriteAllBytesAsync(sfd.FileName, pdfBytes);

                var res = MessageBox.Show(
                    $"Receipt successfully saved!\n\nFile: {sfd.FileName}\n\nWould you like to open it now?",
                    "Download Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download receipt PDF.");
            MessageBox.Show($"Failed to download PDF: {ex.Message}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return false;
    }

    /// <summary>
    /// Generates the release slip PDF and prompts user to save / download.
    /// </summary>
    public static async Task<bool> DownloadReleaseSlipPdfAsync(IReceiptService receiptService, Guid loanId, string loanNumber, Window? owner = null)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Title = "Download Release Slip (மீட்பு சீட்டு) PDF",
                Filter = "PDF Document (*.pdf)|*.pdf|All Files (*.*)|*.*",
                FileName = $"JGF_Release_Slip_{loanNumber.Replace('/', '_')}_{DateTime.Today:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog(owner) == true)
            {
                var pdfBytes = await receiptService.GenerateReleaseSlipPdfAsync(loanId);
                await File.WriteAllBytesAsync(sfd.FileName, pdfBytes);

                var res = MessageBox.Show(
                    $"Release Slip successfully saved!\n\nFile: {sfd.FileName}\n\nWould you like to open it now?",
                    "Download Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download release slip PDF.");
            MessageBox.Show($"Failed to download Release Slip: {ex.Message}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return false;
    }
}
