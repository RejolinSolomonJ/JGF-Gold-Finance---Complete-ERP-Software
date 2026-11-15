using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using JGF.Domain.Entities;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Serilog;

namespace JGF.Desktop.Common;

/// <summary>
/// Generates print visuals and vector/rendered PDFs for Tamil Nadu Pawn Tickets & Jewel Mortgage Deeds (நகை அடமானக் கடன் பத்திரம் & ரசீது).
/// </summary>
public static class LoanPawnTicketPrintHelper
{
    private static readonly System.Windows.Media.FontFamily DefaultFont = new("Segoe UI, Nirmala UI, Noto Sans Tamil, Arial");
    private static readonly SolidColorBrush InkDark = new(System.Windows.Media.Color.FromRgb(0x0F, 0x17, 0x2A));
    private static readonly SolidColorBrush HeaderBlue = new(System.Windows.Media.Color.FromRgb(0x0B, 0x25, 0x45));
    private static readonly SolidColorBrush TableBorderBrush = new(System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55));
    private static readonly SolidColorBrush LightGrayBg = new(System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFC));

    public const string FirmName = "JEBA GOLD FINANCE";
    public const string FirmType = "(Pawn Brokers)";
    public const string FirmAddress = "Pallam Road, Near Bus Stop, Thengamputhur Junction.";
    public const string FirmLicense = "L.No: 2020221223102";
    public const string FirmPhone = "Cell: 8531002850";

    /// <summary>
    /// Sheet 1: Jewel Inventory Record (Office Copy) + Pledged Jewel Receipt Slip (Customer Copy)
    /// </summary>
    public static FrameworkElement CreateInventoryAndSlipVisual(Loan loan, Customer customer, Scheme scheme)
    {
        var container = new Border
        {
            Width = 920,
            Height = 600,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12)
        };
        TextElement.SetFontFamily(container, DefaultFont);

        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(530) }); // Left: Inventory & Declaration
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });  // Perforation
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) }); // Right: Counterfoil / Customer Slip
        container.Child = rootGrid;

        // ══════════════════════════════════════════════════════════════════════
        // LEFT HALF: Office Jewel Inventory & Declaration
        // ══════════════════════════════════════════════════════════════════════
        var leftPanel = new DockPanel { LastChildFill = true };
        Grid.SetColumn(leftPanel, 0);
        rootGrid.Children.Add(leftPanel);

        // Header
        var leftHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(leftHeader, Dock.Top);
        leftPanel.Children.Add(leftHeader);

        leftHeader.Children.Add(new TextBlock
        {
            Text = $"{FirmName} {FirmType}",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = HeaderBlue
        });
        leftHeader.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10,
            Foreground = InkDark
        });

        var lnoDock = new DockPanel { Margin = new Thickness(0, 1, 0, 4) };
        lnoDock.Children.Add(new TextBlock { Text = FirmLicense, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkDark });
        var phText = new TextBlock { Text = FirmPhone, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkDark };
        DockPanel.SetDock(phText, Dock.Right);
        lnoDock.Children.Add(phText);
        leftHeader.Children.Add(lnoDock);

        // Top Metadata Box
        var metaBorder = new Border
        {
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            Background = LightGrayBg,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        DockPanel.SetDock(metaBorder, Dock.Top);
        leftPanel.Children.Add(metaBorder);

        var metaGrid = new Grid();
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metaGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        metaGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        metaGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddLeftMeta(int r, int c, string label, string val, bool bold = false)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 4, 1) };
            p.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, FontSize = 10, Foreground = InkDark });
            p.Children.Add(new TextBlock { Text = " " + val, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = 10, Foreground = InkDark });
            Grid.SetRow(p, r);
            Grid.SetColumn(p, c);
            metaGrid.Children.Add(p);
        }

        AddLeftMeta(0, 0, "Loan No:", loan.LoanNumber, true);
        AddLeftMeta(0, 1, "Customer ID:", customer.Code, true);
        AddLeftMeta(1, 0, "Loan Date:", loan.PledgeDate.ToString("dd-MMM-yyyy") + " " + loan.CreatedAtUtc.ToLocalTime().ToString("hh:mm tt"));
        AddLeftMeta(1, 1, "Scheme:", scheme.Name);
        AddLeftMeta(2, 0, "Address:", $"{customer.Name}, {customer.Address}");
        AddLeftMeta(2, 1, "Amount:", $"₹ {loan.LoanAmount:N2} /-", true);
        metaBorder.Child = metaGrid;

        // Bottom Declaration & Signatures
        var leftBottom = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(leftBottom, Dock.Bottom);
        leftPanel.Children.Add(leftBottom);

        var nomineeDisplay = string.IsNullOrWhiteSpace(loan.NomineeName) ? "வாரிசு" : loan.NomineeName;
        var nomineeRelDisplay = string.IsNullOrWhiteSpace(loan.NomineeRelationship) ? "" : $" ({loan.NomineeRelationship})";

        var declText = new TextBlock
        {
            Text = $"மேலே விவரிக்கும் அடமானப் பொருட்கள் முற்றிலும் எனக்குச் சொந்தமானவை. அதன்மேல் வேறு எவருக்கும் அவகாசமில்லை என்றும் சுத்தமான தங்க நகைகள் என்றும் எனது ஜீவித காலத்திற்குப்பின் முதலும் வட்டியையும் வாங்கிக்கொண்டு மேற்படி உறுப்படிகளை எனது வாரிசான திரு/திருமதி {nomineeDisplay}{nomineeRelDisplay} வசம் கொடுத்துவிடலாமென்று நான் உறுதி கூறுகிறேன்.",
            FontSize = 9.5,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 14,
            Foreground = InkDark,
            Margin = new Thickness(0, 4, 0, 12)
        };
        leftBottom.Children.Add(declText);

        var sigDock = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        sigDock.Children.Add(new TextBlock
        {
            Text = "கடன்தாரர் கையொப்பம்",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Foreground = InkDark
        });
        var mgrText = new TextBlock
        {
            Text = "Manager / Director",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Foreground = InkDark
        };
        DockPanel.SetDock(mgrText, Dock.Right);
        sigDock.Children.Add(mgrText);
        leftBottom.Children.Add(sigDock);

        // Center: Items Grid
        var itemsBorder = new Border
        {
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.White
        };
        leftPanel.Children.Add(itemsBorder);

        var itemsDock = new DockPanel { LastChildFill = true };
        itemsBorder.Child = itemsDock;

        // Table Header
        var thBorder = new Border
        {
            Background = LightGrayBg,
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(4, 3, 4, 3)
        };
        DockPanel.SetDock(thBorder, Dock.Top);
        itemsDock.Children.Add(thBorder);

        var thGrid = new Grid();
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // JType
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });  // Qty
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // G.Weight
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // N.Weight

        void AddTh(int c, string t, TextAlignment align = TextAlignment.Left)
        {
            var tb = new TextBlock { Text = t, FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = align, Foreground = InkDark };
            Grid.SetColumn(tb, c);
            thGrid.Children.Add(tb);
        }
        AddTh(0, "JType");
        AddTh(1, "Jewel Name");
        AddTh(2, "Qty", TextAlignment.Center);
        AddTh(3, "G.Weight", TextAlignment.Right);
        AddTh(4, "N.Weight", TextAlignment.Right);
        thBorder.Child = thGrid;

        // Table Totals Footer
        var tfBorder = new Border
        {
            Background = LightGrayBg,
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(4, 3, 4, 3)
        };
        DockPanel.SetDock(tfBorder, Dock.Bottom);
        itemsDock.Children.Add(tfBorder);

        var tfGrid = new Grid();
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        var items = loan.Items?.ToList() ?? new List<LoanItem>();
        var totalQty = items.Count > 0 ? items.Sum(i => i.Quantity) : 1;
        var totalGross = items.Count > 0 ? items.Sum(i => i.GrossWeight) : loan.GrossWeight;
        var totalNet = items.Count > 0 ? items.Sum(i => i.NetWeight) : loan.NetWeight;

        var tfLabel = new TextBlock { Text = "TOTAL:", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkDark };
        Grid.SetColumn(tfLabel, 1);
        tfGrid.Children.Add(tfLabel);

        var tfQty = new TextBlock { Text = totalQty.ToString(), FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Center, Foreground = InkDark };
        Grid.SetColumn(tfQty, 2);
        tfGrid.Children.Add(tfQty);

        var tfG = new TextBlock { Text = $"{totalGross:F3}", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkDark };
        Grid.SetColumn(tfG, 3);
        tfGrid.Children.Add(tfG);

        var tfN = new TextBlock { Text = $"{totalNet:F3}", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkDark };
        Grid.SetColumn(tfN, 4);
        tfGrid.Children.Add(tfN);
        tfBorder.Child = tfGrid;

        // Item Rows
        var itemsStack = new StackPanel();
        itemsDock.Children.Add(itemsStack);

        int rowIndex = 0;
        foreach (var item in items)
        {
            var rowGrid = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            var typeName = item.JewelType?.Name ?? "Gold Jewel";
            var desc = string.IsNullOrWhiteSpace(item.Description) ? typeName : item.Description;

            void AddRowCell(int c, string val, TextAlignment a = TextAlignment.Left)
            {
                var tb = new TextBlock { Text = val, FontSize = 9.5, TextAlignment = a, Foreground = InkDark, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(tb, c);
                rowGrid.Children.Add(tb);
            }

            AddRowCell(0, typeName);
            AddRowCell(1, desc);
            AddRowCell(2, item.Quantity.ToString(), TextAlignment.Center);
            AddRowCell(3, $"{item.GrossWeight:F3}", TextAlignment.Right);
            AddRowCell(4, $"{item.NetWeight:F3}", TextAlignment.Right);

            itemsStack.Children.Add(rowGrid);
            rowIndex++;
            if (rowIndex >= 6) break; // Keep visual tight
        }


        // ══════════════════════════════════════════════════════════════════════
        // CENTER: Perforation Line
        // ══════════════════════════════════════════════════════════════════════
        var perfPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(perfPanel, 1);
        rootGrid.Children.Add(perfPanel);

        var perfLine = new System.Windows.Shapes.Line
        {
            X1 = 0, Y1 = 0, X2 = 0, Y2 = 570,
            Stroke = TableBorderBrush,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            StrokeThickness = 1
        };
        perfPanel.Children.Add(perfLine);


        // ══════════════════════════════════════════════════════════════════════
        // RIGHT HALF: Customer Counterfoil (ஈடு வைத்த நகைக்கு ரசீது)
        // ══════════════════════════════════════════════════════════════════════
        var rightPanel = new DockPanel { LastChildFill = true };
        Grid.SetColumn(rightPanel, 2);
        rootGrid.Children.Add(rightPanel);

        var rightHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(rightHeader, Dock.Top);
        rightPanel.Children.Add(rightHeader);

        rightHeader.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = HeaderBlue,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        rightHeader.Children.Add(new TextBlock
        {
            Text = "(அரசு அங்கீகாரம் பெற்றது)",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = InkDark,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        rightHeader.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 9.5,
            Foreground = InkDark,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        rightHeader.Children.Add(new TextBlock
        {
            Text = $"{FirmLicense} | {FirmPhone}",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = InkDark,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 4)
        });

        // Title: ஈடு வைத்த நகைக்கு ரசீது
        var rightTitleBorder = new Border
        {
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            Background = LightGrayBg,
            Padding = new Thickness(4, 3, 4, 3),
            Margin = new Thickness(0, 0, 0, 6)
        };
        rightTitleBorder.Child = new TextBlock
        {
            Text = "ஈடு வைத்த நகைக்கு ரசீது",
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = HeaderBlue,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        rightHeader.Children.Add(rightTitleBorder);

        // Pre-filled Slip Details
        var slipGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        for (int i = 0; i < 7; i++) slipGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        slipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        slipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void AddSlipRow(int r, string lbl, string val, bool isBold = false)
        {
            var l = new TextBlock { Text = lbl, FontWeight = FontWeights.SemiBold, FontSize = 10, Foreground = InkDark, Margin = new Thickness(0, 1.5, 0, 1.5) };
            var v = new TextBlock { Text = ": " + val, FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal, FontSize = 10, Foreground = InkDark, Margin = new Thickness(0, 1.5, 0, 1.5) };
            Grid.SetRow(l, r); Grid.SetColumn(l, 0); slipGrid.Children.Add(l);
            Grid.SetRow(v, r); Grid.SetColumn(v, 1); slipGrid.Children.Add(v);
        }

        AddSlipRow(0, "Customer ID", customer.Code, true);
        AddSlipRow(1, "Loan No", loan.LoanNumber, true);
        AddSlipRow(2, "Loan Date", loan.PledgeDate.ToString("dd-MMM-yyyy"));
        AddSlipRow(3, "Address", $"{customer.Name}, {customer.Address}");
        AddSlipRow(4, "Scheme", scheme.Name);
        AddSlipRow(5, "Amount", $"₹ {loan.LoanAmount:N2} /-", true);
        AddSlipRow(6, "Jewel Details", $"Qty: {totalQty} | Net: {totalNet:F3} g", true);

        rightHeader.Children.Add(slipGrid);

        // Due Date Box
        var dueBox = new Border
        {
            Background = LightGrayBg,
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 2, 0, 6)
        };
        var dueStack = new StackPanel();
        dueStack.Children.Add(new TextBlock
        {
            Text = $"திருப்ப வேண்டிய கடைசி தேதி : {loan.MaturityDate:dd-MMM-yyyy}",
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            Foreground = HeaderBlue,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        dueBox.Child = dueStack;
        rightHeader.Children.Add(dueBox);

        // Timings and Rules Box
        var timingBox = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(timingBox, Dock.Bottom);
        rightPanel.Children.Add(timingBox);

        void AddTiming(string t) => timingBox.Children.Add(new TextBlock { Text = t, FontSize = 8.5, Foreground = InkDark, Margin = new Thickness(0, 1, 0, 1) });
        AddTiming("* அலுவலக நேரம்: காலை 9.30 மணி முதல் மாலை 5.30 மணி வரை");
        AddTiming("* நகை திருப்பும் நேரம்: காலை 9.30 மணி முதல் மாலை 4.00 மணி வரை");
        AddTiming("* சனிக்கிழமை நேரம்: காலை 9.30 மணி முதல் மதியம் 12.30 மணி வரை");
        AddTiming("* ஞாயிறு விடுமுறை");

        timingBox.Children.Add(new TextBlock
        {
            Text = $"For {FirmName}",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 4, 0),
            Foreground = HeaderBlue
        });

        return container;
    }

    /// <summary>
    /// Sheet 2: Tamil Legal Mortgage Deed (நகை அடமானக் கடன் பத்திரம்) & Repayment Ledger
    /// </summary>
    public static FrameworkElement CreateMortgageDeedVisual(Loan loan, Customer customer, Scheme scheme, List<Receipt>? pastReceipts = null)
    {
        var container = new Border
        {
            Width = 800,
            Height = 1060,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 20, 24, 20)
        };
        TextElement.SetFontFamily(container, DefaultFont);

        var stack = new StackPanel();
        container.Child = stack;

        // Header
        stack.Children.Add(new TextBlock
        {
            Text = $"{FirmName} {FirmType}",
            FontWeight = FontWeights.Bold,
            FontSize = 17,
            Foreground = HeaderBlue,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 11,
            Foreground = InkDark,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        });

        // Deed Title
        stack.Children.Add(new TextBlock
        {
            Text = "நகை அடமானக் கடன் பத்திரம்",
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = HeaderBlue,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 10),
            TextDecorations = TextDecorations.Underline
        });

        // Top Info Row
        var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        topRow.Children.Add(new TextBlock
        {
            Text = $"G.L. No : {loan.LoanNumber}",
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = HeaderBlue
        });
        var dateText = new TextBlock
        {
            Text = $"தேதி : {loan.PledgeDate:dd-MM-yyyy}",
            FontWeight = FontWeights.SemiBold,
            FontSize = 11.5,
            Foreground = InkDark
        };
        DockPanel.SetDock(dateText, Dock.Right);
        topRow.Children.Add(dateText);
        stack.Children.Add(topRow);

        // Pre-filled Deed Body
        var wordsAmount = AmountToWordsHelper.ToIndianRupees(loan.LoanAmount);
        var guardian = string.IsNullOrWhiteSpace(customer.FatherOrHusbandName) ? "________________" : customer.FatherOrHusbandName;
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "வாரிசு" : loan.NomineeName;

        var p1 = new TextBlock
        {
            FontSize = 11,
            LineHeight = 20,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkDark,
            Margin = new Thickness(0, 0, 0, 6)
        };
        p1.Inlines.Add(new Run("Thengamputhur "));
        p1.Inlines.Add(new Bold(new Run(FirmName)));
        p1.Inlines.Add(new Run($" நிறுவனம் அவர்களுக்கு மேற்படி நிறுவனத்தில் கடன் தொகை ரூபாய் "));
        p1.Inlines.Add(new Bold(new Run($"₹ {loan.LoanAmount:N2} /-")));
        p1.Inlines.Add(new Run($"\nஉறுப்பினர் எண்: "));
        p1.Inlines.Add(new Bold(new Run($"{customer.Code}")));
        p1.Inlines.Add(new Run($"   திரு/திருமதி: "));
        p1.Inlines.Add(new Bold(new Run($"{customer.Name}")));
        p1.Inlines.Add(new Run($"   ஊர்: "));
        p1.Inlines.Add(new Bold(new Run($"{customer.Address}")));
        p1.Inlines.Add(new Run($"\nமகன் / மகள் / கணவர் / மனைவி: "));
        p1.Inlines.Add(new Bold(new Run($"{guardian}")));
        p1.Inlines.Add(new Run(" எழுதிக் கொடுத்த நகை அடமான கடன் பத்திரமாவது."));
        stack.Children.Add(p1);

        var p2 = new TextBlock
        {
            FontSize = 11,
            LineHeight = 19,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkDark,
            Margin = new Thickness(0, 0, 0, 6)
        };
        p2.Inlines.Add(new Run("நான் மேற்படி நிறுவனத்தில் இருந்து பின் பக்கம் குறிப்பிட்டிருக்கிற நடைக்கு கடனாகப் பெற்றுக் கொண்ட தொகை ரூபாய் "));
        p2.Inlines.Add(new Bold(new Run($"₹ {loan.LoanAmount:N2} /- ")));
        p2.Inlines.Add(new Run($"(Rupees {wordsAmount})."));
        stack.Children.Add(p2);

        var p3 = new TextBlock
        {
            FontSize = 10.5,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkDark,
            Margin = new Thickness(0, 0, 0, 8)
        };
        p3.Inlines.Add(new Run("இந்த கடன் தொகைக்கு ரசீதில் குறிப்பிட்டிருக்கிற வட்டியை மூன்று மாதத்திற்கு ஒருமுறை தந்து 12 மாதத்திற்குள் முதலையும் வட்டியையும் செலுத்தி அடமான நகையை திரும்பப் பெற்றுக்கொள்கிறேன். இவ்வாறு செய்ய தவறினால் பாக்கி இருக்கும் கடன் தொகைக்கு கூடுதலாக 3 சமானம் அபராத வட்டி கொடுக்கவும் என்னுடைய அறிவிப்போ சம்மதமோ இல்லாமல் அடமான நகையை விற்பனை செய்து இந்த கடன் தொகையையும் மற்றும் செலவுகளையும் கழித்து மீதி ஏதாவது வருமானால் அதை என்னிடமிருந்து அல்லது என்னுடைய இதர ஸ்தாவர ஜங்கம சொத்துக்களிலிருந்து வசூல் செய்கிறதுக்கும் இதனால் அதிகாரப்படுத்தியிருக்கிறேன். மேற்படி நிறுவனத்தில் இப்போது நடைமுறையிலிருக்கும் நிபந்தனைகளுக்கும் இனி புதிதாய் ஏற்படும் எல்லா நிபந்தனைகளுக்கும் கட்டுப்படுகிறேன்."));
        stack.Children.Add(p3);

        var p4 = new TextBlock
        {
            FontSize = 10.5,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkDark,
            Margin = new Thickness(0, 0, 0, 10)
        };
        p4.Inlines.Add(new Run($"அடமானம் வைத்திருக்கும் நகை முற்றிலும் எனக்குச் சொந்தமானது என்றும், இதன் மேல் வேறு எவருக்கும் அவகாசமில்லை என்றும், இது சுத்தமான தங்க நகைகள் என்றும் உறுதி செய்கிறேன். தங்களிடம் வைத்திருக்கும் இந்த நகையை திரும்ப பெற நேராக வருவதற்கு ஏதாவது அசௌகரியம் ஏற்படுமாகில் என்னுடைய பிற்கால அவகாசியான திரு/திருமதி {nominee} வசம் தக்க ஆதாரத்துடன் முதலும் வட்டியும் வாங்கிக் கொண்டு அடமான நகையை கொடுத்து விடலாமென்று நான் உறுதி கூறுகிறேன்."));
        stack.Children.Add(p4);

        // Signatures Block
        var sigGrid = new Grid { Margin = new Thickness(0, 4, 0, 12) };
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sigB = new TextBlock { Text = "கடன் வாங்கியவர் கையொப்பம் : _________________", FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = InkDark };
        Grid.SetColumn(sigB, 0); sigGrid.Children.Add(sigB);

        var sigR = new TextBlock
        {
            Text = "முதலும் வட்டியும் செலுத்தி நகையை சரிபார்த்துப் பெற்றுக்கொண்டேன்:\nகையொப்பம் : _________________",
            FontWeight = FontWeights.SemiBold,
            FontSize = 10,
            Foreground = InkDark,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(sigR, 1); sigGrid.Children.Add(sigR);
        stack.Children.Add(sigGrid);

        // Divider
        stack.Children.Add(new Border { Height = 1, Background = TableBorderBrush, Margin = new Thickness(0, 0, 0, 8) });

        // Repayment Installments Table Title
        stack.Children.Add(new TextBlock
        {
            Text = $"{FirmName} — தவணை & வட்டி வரவு அட்டவணை (Repayment Ledger)",
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Foreground = HeaderBlue,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Repayment Table Grid
        var repBorder = new Border
        {
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        stack.Children.Add(repBorder);

        var repStack = new StackPanel();
        repBorder.Child = repStack;

        // Table Header
        var repHdr = new Grid { Background = LightGrayBg, Margin = new Thickness(4, 3, 4, 3) };
        repHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // Date
        repHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Interest
        repHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Principal
        repHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Balance
        repHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // Sign
        repHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Date Brought

        void AddRepCol(int c, string text, TextAlignment a = TextAlignment.Left)
        {
            var tb = new TextBlock { Text = text, FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = a, Foreground = InkDark };
            Grid.SetColumn(tb, c);
            repHdr.Children.Add(tb);
        }
        AddRepCol(0, "தேதி");
        AddRepCol(1, "வட்டி வரவு", TextAlignment.Right);
        AddRepCol(2, "அசல் வரவு", TextAlignment.Right);
        AddRepCol(3, "அசல் பாக்கி", TextAlignment.Right);
        AddRepCol(4, "கையொப்பம்", TextAlignment.Center);
        AddRepCol(5, "கொண்டு வந்த தேதி");
        repStack.Children.Add(repHdr);

        // Pre-fill existing receipts or blank lines
        int linesDrawn = 0;
        decimal currentBalance = loan.LoanAmount;

        if (pastReceipts != null)
        {
            foreach (var r in pastReceipts.OrderBy(x => x.ReceiptDate))
            {
                currentBalance -= r.PrincipalAmount;
                var row = new Grid { Margin = new Thickness(4, 2, 4, 2) };
                for (int i = 0; i < 6; i++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = repHdr.ColumnDefinitions[i].Width });

                void AddRowCell(int c, string v, TextAlignment a = TextAlignment.Left)
                {
                    var tb = new TextBlock { Text = v, FontSize = 9, TextAlignment = a, Foreground = InkDark };
                    Grid.SetColumn(tb, c); row.Children.Add(tb);
                }
                AddRowCell(0, r.ReceiptDate.ToString("dd-MM-yyyy"));
                AddRowCell(1, $"₹ {r.InterestAmount:N2}", TextAlignment.Right);
                AddRowCell(2, $"₹ {r.PrincipalAmount:N2}", TextAlignment.Right);
                AddRowCell(3, $"₹ {Math.Max(0m, currentBalance):N2}", TextAlignment.Right);
                AddRowCell(4, "Verified", TextAlignment.Center);
                AddRowCell(5, r.CreatedAtUtc.ToLocalTime().ToString("dd-MM-yyyy"));

                repStack.Children.Add(row);
                linesDrawn++;
            }
        }

        // Draw blank rows up to 7 total rows
        for (int i = linesDrawn; i < 7; i++)
        {
            var blankRow = new Grid { Margin = new Thickness(4, 3, 4, 3) };
            for (int c = 0; c < 6; c++) blankRow.ColumnDefinitions.Add(new ColumnDefinition { Width = repHdr.ColumnDefinitions[c].Width });

            var dotLine = new TextBlock { Text = ". . . . . . . . .", FontSize = 8, Foreground = System.Windows.Media.Brushes.DarkGray };
            Grid.SetColumn(dotLine, 0); blankRow.Children.Add(dotLine);

            repStack.Children.Add(blankRow);
        }

        // Terms and Conditions Section
        stack.Children.Add(new TextBlock
        {
            Text = "விதிமுறைகள் மற்றும் நிபந்தனைகள் :",
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = HeaderBlue,
            Margin = new Thickness(0, 0, 0, 3)
        });

        void AddRule(string r) => stack.Children.Add(new TextBlock
        {
            Text = r,
            FontSize = 9.5,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkDark,
            Margin = new Thickness(0, 1, 0, 1)
        });

        AddRule("1. அடகு வைப்பவர் தனக்கு சொந்தமில்லாத நகையை அடகு வைக்க கூடாது.");
        AddRule("2. அடகு சீட்டினை தவறவிட்டால் ரூ.50 பத்திரத்தில் உறுதிமொழி எழுதிக் கொடுத்து அடகு வைத்த நகையை திரும்பப் பெற்றுக் கொள்ளலாம்.");
        AddRule("3. விண்ணப்பத்தில் தரப்பட்டுள்ள விலாசம் மற்றும் தொலைபேசி எண் இவற்றில் மாற்றம் ஏற்பட்டாலோ நிறுவனத்துக்கு நேரிலோ அல்லது ரிஜிஸ்டர் போஸ்ட் மூலமாக தெரிவிக்க வேண்டும். இல்லையென்றால் விண்ணப்பத்தில் கொடுக்கப்பட்ட விலாசத்திற்கு அனுப்பப்படும் நோட்டீஸ் கடன் வாங்கிய நபர் கைபற்றியதாக கருதப்படும்.");
        AddRule("4. 1 வருடத்தில் நகையை திரும்ப பெறாவிட்டால் ஏலம் செய்து பணத்தை ஈடுசெய்து கொள்ளப்படும்.");

        stack.Children.Add(new TextBlock
        {
            Text = "நகையை நல்ல முறையில் பெற்றுக்கொண்டேன். கையொப்பம் : ______________________",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Foreground = InkDark,
            Margin = new Thickness(0, 8, 0, 0)
        });

        return container;
    }

    /// <summary>
    /// Sends the visual directly to a physical or system printer via PrintDialog.
    /// </summary>
    public static bool PrintTicket(FrameworkElement visual, string jobTitle, Window? owner = null)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                visual.Measure(new System.Windows.Size(printDlg.PrintableAreaWidth, double.PositiveInfinity));
                visual.Arrange(new Rect(new System.Windows.Point(0, 0), visual.DesiredSize));
                visual.UpdateLayout();

                printDlg.PrintVisual(visual, jobTitle);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print pawn ticket visual.");
            MessageBox.Show($"Failed to print: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return false;
    }

    /// <summary>
    /// Generates high-resolution PDF document containing Sheet 1 and/or Sheet 2.
    /// </summary>
    public static async Task<bool> DownloadTicketPdfAsync(Loan loan, Customer customer, Scheme scheme, Window? owner = null)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Title = "Download Jewel Loan Pawn Ticket & Deed (ரசீது & கடன் பத்திரம்)",
                Filter = "PDF Document (*.pdf)|*.pdf|All Files (*.*)|*.*",
                FileName = $"JGF_PawnTicket_{loan.LoanNumber.Replace('/', '_')}_{DateTime.Today:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog(owner) != true) return false;

            var visual1 = CreateInventoryAndSlipVisual(loan, customer, scheme);
            var visual2 = CreateMortgageDeedVisual(loan, customer, scheme, loan.Receipts?.ToList());

            using var doc = new PdfDocument();
            doc.Info.Title = $"JGF Pawn Ticket #{loan.LoanNumber}";
            doc.Info.Author = FirmName;
            doc.Info.Subject = "Tamil Nadu Jewel Loan Pawn Ticket & Mortgage Deed";

            // Render Sheet 1 (Landscape)
            RenderVisualToPdfPage(doc, visual1, isLandscape: true);

            // Render Sheet 2 (Portrait)
            RenderVisualToPdfPage(doc, visual2, isLandscape: false);

            using var fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
            doc.Save(fs, false);

            var res = MessageBox.Show(
                $"Jewel Loan Pawn Ticket & Mortgage Deed saved successfully!\n\nFile: {sfd.FileName}\n\nWould you like to open it now?",
                "Download Complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate pawn ticket PDF.");
            MessageBox.Show($"Failed to download PDF: {ex.Message}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static void RenderVisualToPdfPage(PdfDocument doc, FrameworkElement visual, bool isLandscape)
    {
        visual.Measure(new System.Windows.Size(visual.Width, visual.Height));
        visual.Arrange(new Rect(0, 0, visual.Width, visual.Height));
        visual.UpdateLayout();

        double dpi = 300;
        double scale = dpi / 96.0;
        int pixelWidth = (int)Math.Ceiling(visual.Width * scale);
        int pixelHeight = (int)Math.Ceiling(visual.Height * scale);

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;

        var page = doc.AddPage();
        if (isLandscape)
        {
            page.Width = XUnit.FromPoint(842); // A4 Landscape width
            page.Height = XUnit.FromPoint(595); // A4 Landscape height
        }
        else
        {
            page.Width = XUnit.FromPoint(595); // A4 Portrait width
            page.Height = XUnit.FromPoint(842); // A4 Portrait height
        }

        using var gfx = XGraphics.FromPdfPage(page);
        using var xImg = XImage.FromStream(ms);
        gfx.DrawImage(xImg, 0, 0, page.Width.Point, page.Height.Point);
    }
}

