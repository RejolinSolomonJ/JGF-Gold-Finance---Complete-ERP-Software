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
/// Generates authentic Tamil Nadu Pawn Ticket & Mortgage Deed documents matching exact physical stationery wording and layout.
/// </summary>
public static class LoanPawnTicketPrintHelper
{
    private static readonly System.Windows.Media.FontFamily DefaultFont = new("Segoe UI, Nirmala UI, Noto Sans Tamil, Arial");
    private static readonly SolidColorBrush InkBlack = System.Windows.Media.Brushes.Black;
    private static readonly SolidColorBrush BorderPenBrush = new(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush SoftGray = new(System.Windows.Media.Color.FromRgb(0x47, 0x55, 0x69));

    public const string FirmName = "VASANTHAM FINANCE";
    public const string FirmType = "(Pawn Brokers)";
    public const string FirmAddress = "Pallam Road, Near Bus Stop, Thengamputhur Junction.";
    public const string FirmLicense = "L.No:2020221223102";
    public const string FirmPhone = "Cell:8531002850";

    /// <summary>
    /// Sheet 1: Jewel Inventory Record & Declaration (Left) + Customer Counterfoil Slip (Right)
    /// </summary>
    public static FrameworkElement CreateInventoryAndSlipVisual(Loan loan, Customer customer, Scheme scheme)
    {
        var items = loan.Items?.ToList() ?? new List<LoanItem>();
        var totalQty = items.Count > 0 ? items.Sum(i => i.Quantity) : 1;
        var totalGross = items.Count > 0 ? items.Sum(i => i.GrossWeight) : loan.GrossWeight;
        var totalNet = items.Count > 0 ? items.Sum(i => i.NetWeight) : loan.NetWeight;
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "________________________" : loan.NomineeName;

        var container = new Border
        {
            Width = 980,
            Height = 640,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14)
        };
        TextElement.SetFontFamily(container, DefaultFont);

        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(560) }); // Left Office Copy
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });  // Perforation
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) }); // Right Counterfoil
        container.Child = rootGrid;

        // ══════════════════════════════════════════════════════════════════════
        // LEFT: Office Jewel Inventory & Nominee Declaration
        // ══════════════════════════════════════════════════════════════════════
        var leftDock = new DockPanel { LastChildFill = true };
        Grid.SetColumn(leftDock, 0);
        rootGrid.Children.Add(leftDock);

        // Header
        var leftHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(leftHeader, Dock.Top);
        leftDock.Children.Add(leftHeader);

        leftHeader.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            Foreground = InkBlack
        });
        leftHeader.Children.Add(new TextBlock
        {
            Text = FirmType,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = InkBlack
        });
        leftHeader.Children.Add(new TextBlock
        {
            Text = FirmLicense,
            FontWeight = FontWeights.SemiBold,
            FontSize = 10,
            Foreground = InkBlack
        });
        leftHeader.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10,
            Foreground = InkBlack,
            Margin = new Thickness(0, 1, 0, 4)
        });

        // Top Metadata Grid
        var metaBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        DockPanel.SetDock(metaBorder, Dock.Top);
        leftDock.Children.Add(metaBorder);

        var metaGrid = new Grid();
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 3; i++) metaGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddLMeta(int r, int c, string lbl, string val, bool bold = false)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 4, 2) };
            sp.Children.Add(new TextBlock { Text = lbl.PadRight(12), FontWeight = FontWeights.SemiBold, FontSize = 10, Foreground = InkBlack });
            sp.Children.Add(new TextBlock { Text = ": " + val, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = 10, Foreground = InkBlack });
            Grid.SetRow(sp, r); Grid.SetColumn(sp, c); metaGrid.Children.Add(sp);
        }

        AddLMeta(0, 0, "Amount", $"₹ {loan.LoanAmount:N2} /-", true);
        AddLMeta(0, 1, "Customer ID", customer.Code, true);
        AddLMeta(1, 0, "Loan No", loan.LoanNumber, true);
        AddLMeta(1, 1, "Scheme", scheme.Name);
        AddLMeta(2, 0, "Loan Date", loan.PledgeDate.ToString("dd-MM-yyyy") + "  " + loan.CreatedAtUtc.ToLocalTime().ToString("hh:mm:ss tt"));
        AddLMeta(2, 1, "Address", $"{customer.Name}, {customer.Address}");
        metaBorder.Child = metaGrid;

        // Bottom Declaration & Signatures
        var leftBottom = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(leftBottom, Dock.Bottom);
        leftDock.Children.Add(leftBottom);

        var declBlock = new TextBlock
        {
            Text = $"மேலே விவரிக்கும் அடமானப் பொருட்கள் முற்றிலும் எனக்குச் சொந்தமானவை. அதன்மேல் வேறு எவருக்கும் அவகாசமில்லை என்றும் சுத்தமான தங்க நகைகள் என்றும் எனது ஜீவித காலத்திற்குப்பின் முதலும் வட்டியையும் வாங்கிக்கொண்டு மேற்படி உறுப்படிகளை எனது கணவர் : மனைவி : தாயார் : மகன் : மகள் : திரு : திருமதி {nominee} வசம் கொடுத்துவிடலாமென்று நான் உறுதி கூறுகிறேன்.",
            FontSize = 9.5,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 4, 0, 14)
        };
        leftBottom.Children.Add(declBlock);

        var sigGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sigBorrower = new TextBlock
        {
            Text = "கடன்தாரர் கையொப்பம்",
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            Foreground = InkBlack
        };
        Grid.SetColumn(sigBorrower, 0);
        sigGrid.Children.Add(sigBorrower);

        var sigMgr = new TextBlock
        {
            Text = "Manager/ Director",
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(sigMgr, 1);
        sigGrid.Children.Add(sigMgr);
        leftBottom.Children.Add(sigGrid);

        // Center Jewel Table
        var tableBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.White
        };
        leftDock.Children.Add(tableBorder);

        var tableDock = new DockPanel { LastChildFill = true };
        tableBorder.Child = tableDock;

        // Header Row
        var thBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(4, 3, 4, 3)
        };
        DockPanel.SetDock(thBorder, Dock.Top);
        tableDock.Children.Add(thBorder);

        var thGrid = new Grid();
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        thGrid.ColumnDefinitions.Add(new GridDefinitionStar());
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
        thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });

        void AddTh(int c, string t, TextAlignment a = TextAlignment.Left)
        {
            var tb = new TextBlock { Text = t, FontWeight = FontWeights.Bold, FontSize = 10, TextAlignment = a, Foreground = InkBlack };
            Grid.SetColumn(tb, c); thGrid.Children.Add(tb);
        }
        AddTh(0, "JType");
        AddTh(1, "Jewel Name");
        AddTh(2, "Qty", TextAlignment.Center);
        AddTh(3, "G.Weight", TextAlignment.Right);
        AddTh(4, "N.Weight", TextAlignment.Right);
        thBorder.Child = thGrid;

        // Totals Footer Row
        var tfBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(4, 3, 4, 3)
        };
        DockPanel.SetDock(tfBorder, Dock.Bottom);
        tableDock.Children.Add(tfBorder);

        var tfGrid = new Grid();
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        tfGrid.ColumnDefinitions.Add(new GridDefinitionStar());
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
        tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });

        var tfL = new TextBlock { Text = "Total Qty", FontWeight = FontWeights.Bold, FontSize = 10, Foreground = InkBlack };
        Grid.SetColumn(tfL, 1); tfGrid.Children.Add(tfL);

        var tfQ = new TextBlock { Text = totalQty.ToString(), FontWeight = FontWeights.Bold, FontSize = 10, TextAlignment = TextAlignment.Center, Foreground = InkBlack };
        Grid.SetColumn(tfQ, 2); tfGrid.Children.Add(tfQ);

        var tfG = new TextBlock { Text = $"{totalGross:F3}", FontWeight = FontWeights.Bold, FontSize = 10, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(tfG, 3); tfGrid.Children.Add(tfG);

        var tfN = new TextBlock { Text = $"{totalNet:F3}", FontWeight = FontWeights.Bold, FontSize = 10, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(tfN, 4); tfGrid.Children.Add(tfN);
        tfBorder.Child = tfGrid;

        // Items Stack
        var rowsStack = new StackPanel();
        tableDock.Children.Add(rowsStack);

        int count = 0;
        foreach (var it in items)
        {
            var rg = new Grid { Margin = new Thickness(4, 3, 4, 3) };
            for (int c = 0; c < 5; c++) rg.ColumnDefinitions.Add(new ColumnDefinition { Width = thGrid.ColumnDefinitions[c].Width });

            var jt = it.JewelType?.Name ?? "Gold Jewel";
            var desc = string.IsNullOrWhiteSpace(it.Description) ? jt : it.Description;

            void AddCell(int c, string v, TextAlignment a = TextAlignment.Left)
            {
                var tb = new TextBlock { Text = v, FontSize = 9.5, TextAlignment = a, Foreground = InkBlack };
                Grid.SetColumn(tb, c); rg.Children.Add(tb);
            }
            AddCell(0, jt);
            AddCell(1, desc);
            AddCell(2, it.Quantity.ToString(), TextAlignment.Center);
            AddCell(3, $"{it.GrossWeight:F3}", TextAlignment.Right);
            AddCell(4, $"{it.NetWeight:F3}", TextAlignment.Right);
            rowsStack.Children.Add(rg);
            count++;
            if (count >= 6) break;
        }


        // ══════════════════════════════════════════════════════════════════════
        // CENTER: Perforation Line
        // ══════════════════════════════════════════════════════════════════════
        var perfBox = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch };
        Grid.SetColumn(perfBox, 1);
        rootGrid.Children.Add(perfBox);

        var vLine = new System.Windows.Shapes.Line
        {
            X1 = 0, Y1 = 0, X2 = 0, Y2 = 610,
            Stroke = BorderPenBrush,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            StrokeThickness = 1
        };
        perfBox.Children.Add(vLine);


        // ══════════════════════════════════════════════════════════════════════
        // RIGHT: Customer Counterfoil (ஈடு வைத்த நகைக்கு ரசீது)
        // ══════════════════════════════════════════════════════════════════════
        var rightDock = new DockPanel { LastChildFill = true };
        Grid.SetColumn(rightDock, 2);
        rootGrid.Children.Add(rightDock);

        var rightHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(rightHeader, Dock.Top);
        rightDock.Children.Add(rightHeader);

        rightHeader.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        rightHeader.Children.Add(new TextBlock
        {
            Text = "(அரசு அங்கீகாரம் பெற்றது)",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        rightHeader.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 9.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        // Title Box: ஈடு வைத்த நகைக்கு ரசீது
        var rTitleBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 3, 4, 3),
            Margin = new Thickness(0, 4, 0, 4)
        };
        rTitleBorder.Child = new TextBlock
        {
            Text = "ஈடு வைத்த நகைக்கு ரசீது",
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        rightHeader.Children.Add(rTitleBorder);

        var rLnoDock = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        rLnoDock.Children.Add(new TextBlock { Text = FirmLicense, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkBlack });
        var rPh = new TextBlock { Text = FirmPhone, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkBlack };
        DockPanel.SetDock(rPh, Dock.Right);
        rLnoDock.Children.Add(rPh);
        rightHeader.Children.Add(rLnoDock);

        // Slip Details Grid
        var slipBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        rightHeader.Children.Add(slipBorder);

        var slipGrid = new Grid();
        slipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        slipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 7; i++) slipGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddSlipRow(int r, string lbl, string val, bool bold = false)
        {
            var l = new TextBlock { Text = lbl, FontWeight = FontWeights.SemiBold, FontSize = 10, Foreground = InkBlack, Margin = new Thickness(0, 1.5, 0, 1.5) };
            var v = new TextBlock { Text = ": " + val, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = 10, Foreground = InkBlack, Margin = new Thickness(0, 1.5, 0, 1.5) };
            Grid.SetRow(l, r); Grid.SetColumn(l, 0); slipGrid.Children.Add(l);
            Grid.SetRow(v, r); Grid.SetColumn(v, 1); slipGrid.Children.Add(v);
        }

        AddSlipRow(0, "Amount", $"₹ {loan.LoanAmount:N2} /-", true);
        AddSlipRow(1, "Customer ID", customer.Code, true);
        AddSlipRow(2, "Scheme", scheme.Name);
        AddSlipRow(3, "Loan No", loan.LoanNumber, true);
        AddSlipRow(4, "Loan Date", loan.PledgeDate.ToString("dd-MM-yyyy") + " " + loan.CreatedAtUtc.ToLocalTime().ToString("hh:mm tt"));
        AddSlipRow(5, "Address", $"{customer.Name}, {customer.Address}");
        AddSlipRow(6, "Jewel Details", $"Total Qty • {totalQty}  |  Net Weight • {totalNet:F3} g", true);
        slipBorder.Child = slipGrid;

        // Due Date Box
        var dueBox = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        dueBox.Child = new TextBlock
        {
            Text = $"திருப்ப வேண்டிய கடைசி தேதி : {loan.MaturityDate:dd-MM-yyyy}",
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        rightHeader.Children.Add(dueBox);

        // Timings and Footer Notice
        var rBottom = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(rBottom, Dock.Bottom);
        rightDock.Children.Add(rBottom);

        rBottom.Children.Add(new TextBlock { Text = $"Ph : {customer.Phone}", FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkBlack, Margin = new Thickness(0, 0, 0, 3) });
        rBottom.Children.Add(new TextBlock { Text = "* அலுவலக நேரம் காலை 9.30 மணி முதல் மாலை 5.30 மணி வரை", FontSize = 9, Foreground = InkBlack });
        rBottom.Children.Add(new TextBlock { Text = "* நகை திருப்பும் நேரம் : காலை 9.30 மணி முதல் மாலை 4 மணி வரை", FontSize = 9, Foreground = InkBlack });
        rBottom.Children.Add(new TextBlock { Text = "* சனிக்கிழமை நேரம் : காலை 9.30 மணி முதல் மதியம் 12.30 மணி வரை", FontSize = 9, Foreground = InkBlack });
        rBottom.Children.Add(new TextBlock { Text = "ஞாயிறு விடுமுறை", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 0) });

        rBottom.Children.Add(new TextBlock
        {
            Text = $"For {FirmName}",
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 4, 0)
        });

        return container;
    }

    /// <summary>
    /// Sheet 2: The exact Legal Mortgage Deed & Repayment Ledger matching Image 1
    /// </summary>
    public static FrameworkElement CreateMortgageDeedVisual(Loan loan, Customer customer, Scheme scheme, List<Receipt>? pastReceipts = null)
    {
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "________________________" : loan.NomineeName;
        var guardian = string.IsNullOrWhiteSpace(customer.FatherOrHusbandName) ? "________________________" : customer.FatherOrHusbandName;
        var wordsAmount = AmountToWordsHelper.ToIndianRupees(loan.LoanAmount);

        var container = new Border
        {
            Width = 980,
            Height = 640,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14)
        };
        TextElement.SetFontFamily(container, DefaultFont);

        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(560) }); // Left: Deed Text & 2 Signature Boxes
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });  // Gap/Divider
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) }); // Right: Repayment Table & Rules Box
        container.Child = rootGrid;

        // ══════════════════════════════════════════════════════════════════════
        // LEFT: நகை அடமானக் கடன் பத்திரம் (Exact wording from Photo 1)
        // ══════════════════════════════════════════════════════════════════════
        var leftDock = new DockPanel { LastChildFill = true };
        Grid.SetColumn(leftDock, 0);
        rootGrid.Children.Add(leftDock);

        // Header
        var leftHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(leftHeader, Dock.Top);
        leftDock.Children.Add(leftHeader);

        var topTitleDock = new DockPanel();
        var topAmt = new TextBlock { Text = $"கடன் தொகை ரூபாய்  ₹ {loan.LoanAmount:N2} /-", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = InkBlack };
        DockPanel.SetDock(topAmt, Dock.Right);
        topTitleDock.Children.Add(topAmt);

        var fnStack = new StackPanel();
        fnStack.Children.Add(new TextBlock { Text = FirmName, FontWeight = FontWeights.Bold, FontSize = 17, Foreground = InkBlack });
        fnStack.Children.Add(new TextBlock { Text = FirmType, FontWeight = FontWeights.SemiBold, FontSize = 10, Foreground = InkBlack });
        fnStack.Children.Add(new TextBlock { Text = FirmAddress, FontSize = 9.5, Foreground = InkBlack });
        topTitleDock.Children.Add(fnStack);
        leftHeader.Children.Add(topTitleDock);

        leftHeader.Children.Add(new TextBlock
        {
            Text = "நகை அடமானக் கடன் பத்திரம்",
            FontWeight = FontWeights.Bold,
            FontSize = 15,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 6)
        });

        // Bottom: Two Signature Boxes
        var leftBottom = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(leftBottom, Dock.Bottom);
        leftDock.Children.Add(leftBottom);

        var sigGrid = new Grid();
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Box 1: Borrower Signature Box
        var sigBox1 = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Height = 70,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var sb1Dock = new DockPanel();
        var sb1Label = new TextBlock
        {
            Text = "கையொப்பம்",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        DockPanel.SetDock(sb1Label, Dock.Bottom);
        sb1Dock.Children.Add(sb1Label);
        sigBox1.Child = sb1Dock;
        Grid.SetColumn(sigBox1, 0);
        sigGrid.Children.Add(sigBox1);

        // Box 2: Jewel Return & Verification Box
        var sigBox2 = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Height = 70,
            Padding = new Thickness(6, 4, 6, 4)
        };
        var sb2Stack = new DockPanel();
        var sb2Text = new TextBlock
        {
            Text = "முதலும், வட்டியும் செலுத்தி பின்பக்கம் குறிப்பிட்டிருக்கிற நகையை சரிபார்த்துத் திரும்பப் பெற்றுக் கொண்டேன்.",
            FontSize = 9,
            LineHeight = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack
        };
        DockPanel.SetDock(sb2Text, Dock.Top);
        sb2Stack.Children.Add(sb2Text);

        var sb2BottomDock = new DockPanel();
        sb2BottomDock.Children.Add(new TextBlock { Text = "கொண்டு வந்த தேதி: ____________", FontSize = 9, Foreground = InkBlack });
        var sb2Sign = new TextBlock { Text = "கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        DockPanel.SetDock(sb2Sign, Dock.Right);
        sb2BottomDock.Children.Add(sb2Sign);
        DockPanel.SetDock(sb2BottomDock, Dock.Bottom);
        sb2Stack.Children.Add(sb2BottomDock);

        sigBox2.Child = sb2Stack;
        Grid.SetColumn(sigBox2, 1);
        sigGrid.Children.Add(sigBox2);

        leftBottom.Children.Add(sigGrid);

        // Center Deed Text (Exact wording from Photo 1)
        var deedScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        leftDock.Children.Add(deedScroll);

        var deedStack = new StackPanel();
        deedScroll.Content = deedStack;

        // Line 1: G.L.No and Date
        var glDock = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
        glDock.Children.Add(new TextBlock { Text = $"G.L.No : {loan.LoanNumber}", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = InkBlack });
        var dt = new TextBlock { Text = $"தேதி : {loan.PledgeDate:dd-MM-yyyy}", FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = InkBlack };
        DockPanel.SetDock(dt, Dock.Right);
        glDock.Children.Add(dt);
        deedStack.Children.Add(glDock);

        // Body Text 1: Thengamputhur VASANTHAM பைனான்ஸ் நிறுவனம் அவர்களுக்கு...
        var p1 = new TextBlock { FontSize = 10, LineHeight = 16.5, TextWrapping = TextWrapping.Wrap, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 3) };
        p1.Inlines.Add(new Run($"தேதி Thengamputhur VASANTHAM பைனான்ஸ் நிறுவனம் அவர்களுக்கு மேற்படி நிறுவனத்தில் கடன் தொகை ரூபாய் "));
        p1.Inlines.Add(new Bold(new Run($"₹ {loan.LoanAmount:N2} /-")));
        p1.Inlines.Add(new Run($"\nஉறுப்பினர் எண் : "));
        p1.Inlines.Add(new Bold(new Run($"{customer.Code}")));
        p1.Inlines.Add(new Run($"\nதிரு/திருமதி : "));
        p1.Inlines.Add(new Bold(new Run($"{customer.Name}")));
        p1.Inlines.Add(new Run($"   ஊர் : "));
        p1.Inlines.Add(new Bold(new Run($"{customer.Address}")));
        p1.Inlines.Add(new Run($"\nமகன் / மகள் / கணவர் / மனைவி : "));
        p1.Inlines.Add(new Bold(new Run($"{guardian}")));
        p1.Inlines.Add(new Run(" எழுதிக் கொடுத்த நகை அடமான கடன் பத்திரமாவது."));
        deedStack.Children.Add(p1);

        // Body Text 2: நான் மேற்படி நிறுவனத்தில் இருந்து...
        var p2 = new TextBlock { FontSize = 10, LineHeight = 16, TextWrapping = TextWrapping.Wrap, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 3) };
        p2.Inlines.Add(new Run("நான் மேற்படி நிறுவனத்தில் இருந்து பின் பக்கம் குறிப்பிட்டிருக்கிற நடைக்கு கடனாகப் பெற்றுக் கொண்ட தொகை ரூபாய் "));
        p2.Inlines.Add(new Bold(new Run($"₹ {loan.LoanAmount:N2} /- ")));
        p2.Inlines.Add(new Run($"(Rupees {wordsAmount})."));
        deedStack.Children.Add(p2);

        // Body Text 3: இந்த கடன் தொகைக்கு ரசீதில் குறிப்பிட்டிருக்கிற வட்டியை...
        var p3 = new TextBlock { FontSize = 9.5, LineHeight = 15, TextWrapping = TextWrapping.Wrap, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 3) };
        p3.Inlines.Add(new Run("இந்த கடன் தொகைக்கு ரசீதில் குறிப்பிட்டிருக்கிற வட்டியை மூன்று மாதத்திற்கு ஒருமுறை தந்து 6 மாதத்திற்குள் முதலையும் வட்டியையும் செலுத்தி அடமான நகையை திரும்பப் பெற்றுக்கொள்கிறேன். இவ்வாறு செய்ய தவறினால் பாக்கி இருக்கும் கடன் தொகைக்கு கூடுதலாக 3 சமானம் அபராத வட்டி கொடுக்கவும் என்னுடைய அறிவிப்போ சம்மதமோ இல்லாமல் அடமான நகையை விற்பனை செய்து , இந்த கடன் தொகையையும் மற்றும் செலவுகளையும் கழித்து மீதி ஏதாவது வருமானால் , அதை என்னிடமிருந்து அல்லது என்னுடைய இதர ஸ்தாவர ஜங்கம சொத்துக்களிலிருந்து வசூல் செய்கிறதுக்கும் இதனால் அதிகாரப்படுத்தியிருக்கிறேன். மேற்படி நிறுவனத்தில் இப்போது நடைமுறையிலிருக்கும் நிபந்தனைகளுக்கும் இனி புதிதாய் ஏற்படும் எல்லா நிபந்தனைகளுக்கும் கட்டுப்படுகிறேன்."));
        deedStack.Children.Add(p3);

        // Body Text 4: அடமானம் வைத்திருக்கும் கடன் முற்றிலும் சொந்தமானது என்றும்...
        var p4 = new TextBlock { FontSize = 9.5, LineHeight = 15, TextWrapping = TextWrapping.Wrap, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 3) };
        p4.Inlines.Add(new Run($"அடமானம் வைத்திருக்கும் கடன் முற்றிலும் சொந்தமானது என்றும், இதன் மேல் வேறு எவருக்கும் அவகாசமில்லை என்றும், இது சுத்தமான தங்க நகைகள் என்றும் உறுதி செய்கிறேன். தங்களிடம் வைத்திருக்கும் இந்த நகையை திரும்ப பெற நேராக வருவதற்கு ஏதாவது அசௌகரியம் ஏற்படுமாகில் என்னுடைய பிற்கால அவகாசியான திரு/திருமதி {nominee} வசம் தக்க ஆதாரத்துடன் முதலும், வட்டியும் வாங்கிக் கொண்டு அடமான நகையை கொடுத்து விடலாமென்று நான் உறுதி கூறுகிறேன்."));
        deedStack.Children.Add(p4);


        // ══════════════════════════════════════════════════════════════════════
        // CENTER: Divider
        // ══════════════════════════════════════════════════════════════════════
        var divBox = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch };
        Grid.SetColumn(divBox, 1);
        rootGrid.Children.Add(divBox);

        var midLine = new System.Windows.Shapes.Line
        {
            X1 = 0, Y1 = 0, X2 = 0, Y2 = 610,
            Stroke = BorderPenBrush,
            StrokeThickness = 1
        };
        divBox.Children.Add(midLine);


        // ══════════════════════════════════════════════════════════════════════
        // RIGHT: Repayment Installment Table & Terms Box (Exact from Photo 1)
        // ══════════════════════════════════════════════════════════════════════
        var rightDock = new DockPanel { LastChildFill = true };
        Grid.SetColumn(rightDock, 2);
        rootGrid.Children.Add(rightDock);

        // Header Title: VASANTHAM FINANCE
        var rHead = new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };
        DockPanel.SetDock(rHead, Dock.Top);
        rightDock.Children.Add(rHead);

        // Bottom Terms Box (Exact from Photo 1)
        var termsBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(termsBorder, Dock.Bottom);
        rightDock.Children.Add(termsBorder);

        var termsStack = new StackPanel();
        termsBorder.Child = termsStack;

        termsStack.Children.Add(new TextBlock
        {
            Text = "விதிமுறைகள் மற்றும் நிபந்தனைகள்",
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        void AddT(string t) => termsStack.Children.Add(new TextBlock
        {
            Text = t,
            FontSize = 8.5,
            LineHeight = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 1.5, 0, 1.5)
        });

        AddT("1. அடகு வைப்பவர் தனக்கு சொந்தமில்லாத நகையை அடகு வைக்க கூடாது");
        AddT("2. அடகு சீட்டினை தவறவிட்டால் ரூ.50 பத்திரத்தில் உறுதிமொழி எழுதிக் கொடுத்து அடகு வைத்த நகையை திரும்பப் பெற்றுக் கொள்ளலாம்");
        AddT("3. விண்ணப்பத்தில் தரப்பட்டுள்ள விலாசம் மற்றும் தொலைபேசி எண் இவற்றில் மாற்றம் ஏற்பட்டாலோ நிறுவனத்துக்கு நேரிலோ அல்லது ரிஜிஸ்டர் போஸ்ட் மூலமாக தெரிவிக்க வேண்டும். இல்லையென்றால் விண்ணப்பத்தில் கொடுக்கப்பட்ட விலாசத்திற்கு அனுப்பப்படும் நோட்டீஸ் கடன் வாங்கி நபர் கைப்பற்றியதாக கருதப்படும்.");
        AddT("4. 1 வருடத்தில் நகையை திரும்ப பெறாவிட்டால் ஏலம் செய்து பணத்தை ஈடுசெய்து கொள்ளப்படும்.");

        var recLine = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        recLine.Children.Add(new TextBlock { Text = "நகையை நல்ல முறையில் பெற்றுக்கொண்டேன்", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = InkBlack });
        var recSign = new TextBlock { Text = "கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        DockPanel.SetDock(recSign, Dock.Right);
        recLine.Children.Add(recSign);
        termsStack.Children.Add(recLine);

        // Center Repayment Table (தேதி | வட்டி வரவு | அசல் வரவு | அசல் பாக்கி | கையொப்பம் | கொண்டு வந்த தேதி)
        var repBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.White
        };
        rightDock.Children.Add(repBorder);

        var repStack = new StackPanel();
        repBorder.Child = repStack;

        // Table Header
        var repTh = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(2, 3, 2, 3)
        };
        repStack.Children.Add(repTh);

        var thG = new Grid();
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) }); // தேதி
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // வட்டி வரவு
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // அசல் வரவு
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // அசல் பாக்கி
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); // கையொப்பம்
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // கொண்டு வந்த தேதி

        void AddThR(int c, string t, TextAlignment a = TextAlignment.Left)
        {
            var tb = new TextBlock { Text = t, FontWeight = FontWeights.Bold, FontSize = 8.5, TextAlignment = a, Foreground = InkBlack };
            Grid.SetColumn(tb, c); thG.Children.Add(tb);
        }
        AddThR(0, "தேதி");
        AddThR(1, "வட்டி வரவு", TextAlignment.Right);
        AddThR(2, "அசல் வரவு", TextAlignment.Right);
        AddThR(3, "அசல் பாக்கி", TextAlignment.Right);
        AddThR(4, "கையொப்பம்", TextAlignment.Center);
        AddThR(5, "கொண்டு வந்த தேதி");
        repTh.Child = thG;

        // Pre-fill existing receipts or blank lines
        int lines = 0;
        decimal bal = loan.LoanAmount;

        if (pastReceipts != null)
        {
            foreach (var r in pastReceipts.OrderBy(x => x.ReceiptDate))
            {
                bal -= r.PrincipalAmount;
                var row = new Grid { Margin = new Thickness(2, 2, 2, 2) };
                for (int c = 0; c < 6; c++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = thG.ColumnDefinitions[c].Width });

                void AddRowC(int c, string v, TextAlignment a = TextAlignment.Left)
                {
                    var tb = new TextBlock { Text = v, FontSize = 8.5, TextAlignment = a, Foreground = InkBlack };
                    Grid.SetColumn(tb, c); row.Children.Add(tb);
                }
                AddRowC(0, r.ReceiptDate.ToString("dd-MM-yyyy"));
                AddRowC(1, $"₹{r.InterestAmount:N0}", TextAlignment.Right);
                AddRowC(2, $"₹{r.PrincipalAmount:N0}", TextAlignment.Right);
                AddRowC(3, $"₹{Math.Max(0m, bal):N0}", TextAlignment.Right);
                AddRowC(4, "Verified", TextAlignment.Center);
                AddRowC(5, r.CreatedAtUtc.ToLocalTime().ToString("dd-MM-yyyy"));
                repStack.Children.Add(row);
                lines++;
            }
        }

        // Draw blank lined rows
        for (int i = lines; i < 9; i++)
        {
            var blank = new Border
            {
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x00, 0x00, 0x00)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 22
            };
            repStack.Children.Add(blank);
        }

        return container;
    }

    /// <summary>
    /// Helper class for star-width grid column
    /// </summary>
    private class GridDefinitionStar : ColumnDefinition
    {
        public GridDefinitionStar()
        {
            Width = new GridLength(1, GridUnitType.Star);
        }
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
    /// Generates high-resolution PDF document containing Sheet 1 and Sheet 2.
    /// </summary>
    public static async Task<bool> DownloadTicketPdfAsync(Loan loan, Customer customer, Scheme scheme, Window? owner = null)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Title = "Download Jewel Loan Pawn Ticket & Deed (ரசீது & கடன் பத்திரம்)",
                Filter = "PDF Document (*.pdf)|*.pdf|All Files (*.*)|*.*",
                FileName = $"VASANTHAM_PawnTicket_{loan.LoanNumber.Replace('/', '_')}_{DateTime.Today:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog(owner) != true) return false;

            var visual1 = CreateInventoryAndSlipVisual(loan, customer, scheme);
            var visual2 = CreateMortgageDeedVisual(loan, customer, scheme, loan.Receipts?.ToList());

            using var doc = new PdfDocument();
            doc.Info.Title = $"VASANTHAM FINANCE Pawn Ticket #{loan.LoanNumber}";
            doc.Info.Author = FirmName;
            doc.Info.Subject = "Tamil Nadu Jewel Loan Pawn Ticket & Mortgage Deed";

            // Both sheets are landscape matching the scanned stationery
            RenderVisualToPdfPage(doc, visual1, isLandscape: true);
            RenderVisualToPdfPage(doc, visual2, isLandscape: true);

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
