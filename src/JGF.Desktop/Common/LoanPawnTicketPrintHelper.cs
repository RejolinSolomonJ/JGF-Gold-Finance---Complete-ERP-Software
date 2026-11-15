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
/// Generates authentic Tamil Nadu Pawn Ticket & Mortgage Deed documents matching exact physical Word document layout.
/// Page 1: நகை அடமானக் கடன் பத்திரம் (Mortgage Deed & Repayment Ledger & Conditions)
/// Page 2: Jewel Inventory Record (Office Copy) + Perforation + ஈடு வைத்த நகைக்கு ரசீது (Customer Slip)
/// </summary>
public static class LoanPawnTicketPrintHelper
{
    private static readonly System.Windows.Media.FontFamily DefaultFont = new("Segoe UI, Nirmala UI, Noto Sans Tamil, Arial");
    private static readonly SolidColorBrush InkBlack = System.Windows.Media.Brushes.Black;
    private static readonly SolidColorBrush BorderPenBrush = new(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush LightDividerBrush = new(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));

    public const string FirmName = "JEBA GOLD FINANCE";
    public const string FirmType = "(Pawn Brokers)";
    public const string FirmAddress = "South Thamaraikulam, Kanyakumari .";
    public const string FirmPhone = "Cell : 9360557680";

    // Standard A4 Portrait dimensions at 96 DPI: 794 x 1123
    public const double A4Width = 794;
    public const double A4Height = 1123;

    /// <summary>
    /// Sheet 1: நகை அடமானக் கடன் பத்திரம் (Legal Mortgage Deed, Repayment Table, Conditions) — Matches Page 1 of Word Document
    /// </summary>
    public static FrameworkElement CreateMortgageDeedVisual(Loan loan, Customer customer, Scheme scheme, List<Receipt>? pastReceipts = null)
    {
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "________________________" : loan.NomineeName;
        var guardian = string.IsNullOrWhiteSpace(customer.FatherOrHusbandName) ? "________________________" : customer.FatherOrHusbandName;
        var wordsAmount = AmountToWordsHelper.ToIndianRupees(loan.LoanAmount);

        var container = new Border
        {
            Width = A4Width,
            Height = A4Height,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(36, 28, 36, 28)
        };
        TextElement.SetFontFamily(container, DefaultFont);

        var stack = new StackPanel();
        container.Child = stack;

        // ── 1. Top Header ──
        stack.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 20,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = FirmType,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 1)
        });
        stack.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "நகை அடமானக் கடன் பத்திரம்",
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 6)
        });

        // ── 2. Top Table: G.L.No | தேதி | கடன் தொகை ரூபாய் ──
        var topTbl = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var tg = new Grid();
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var c1 = new TextBlock { Text = $"G.L.No : {loan.LoanNumber}", FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = InkBlack };
        var c2 = new TextBlock { Text = $"தேதி : {loan.PledgeDate:dd-MM-yyyy}", FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = InkBlack };
        var c3 = new TextBlock { Text = $"கடன் தொகை ரூபாய் : ₹ {loan.LoanAmount:N2} /-", FontWeight = FontWeights.Bold, FontSize = 10.5, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(c1, 0); tg.Children.Add(c1);
        Grid.SetColumn(c2, 1); tg.Children.Add(c2);
        Grid.SetColumn(c3, 2); tg.Children.Add(c3);
        topTbl.Child = tg;
        stack.Children.Add(topTbl);

        // ── 3. Customer & Guardian Info ──
        var pInfo1 = new TextBlock { FontSize = 10, LineHeight = 16, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 2) };
        pInfo1.Inlines.Add(new Run("உறுப்பினர் எண்: "));
        pInfo1.Inlines.Add(new Bold(new Run(customer.Code)));
        pInfo1.Inlines.Add(new Run("                ஊர்: "));
        pInfo1.Inlines.Add(new Bold(new Run(customer.Address)));
        stack.Children.Add(pInfo1);

        var pInfo2 = new TextBlock { FontSize = 10, LineHeight = 16, Foreground = InkBlack, Margin = new Thickness(0, 0, 0, 4) };
        pInfo2.Inlines.Add(new Run("திரு/திருமதி: "));
        pInfo2.Inlines.Add(new Bold(new Run(customer.Name)));
        pInfo2.Inlines.Add(new Run("   மகன்/மகள்/கணவர்/மனைவி: "));
        pInfo2.Inlines.Add(new Bold(new Run(guardian)));
        stack.Children.Add(pInfo2);

        // ── 4. Legal Deed Tamil Body Text (Exact from Word Document) ──
        stack.Children.Add(new TextBlock
        {
            Text = "எழுதிக் கொடுத்த நகை அடமான கடன் பத்திரமாவது.",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Foreground = InkBlack,
            Margin = new Thickness(0, 2, 0, 2)
        });

        var pDeed = new TextBlock
        {
            FontSize = 9.5,
            LineHeight = 15.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 1, 0, 4)
        };
        pDeed.Inlines.Add(new Run("நான் மேற்படி நிறுவனத்தில் இருந்து பின்பக்கம் குறிப்பிட்டிருக்கும் நகைக்கு கடனாகப் பெற்றுக் கொண்ட தொகை ரூபாய் "));
        pDeed.Inlines.Add(new Bold(new Run($"₹ {loan.LoanAmount:N2} /-")));
        pDeed.Inlines.Add(new Run($" (Rupees {wordsAmount}) "));
        pDeed.Inlines.Add(new Run("இந்த கடன் தொகைக்கு ரசீதில் குறிப்பிட்டிருக்கிற வட்டியை மூன்று மாதத்திற்கு ஒருமுறை தந்தும், 6 மாதத்திற்குள் முதலும் பாக்கி வட்டியும் திருப்பிச் செலுத்தி அடமான நகையை திரும்பப் பெற்றுக்கொள்கிறேன். இவ்வாறு தவறினால் கடன் பாக்கி இருக்கும் தொகைக்கு எனக்கு அறிவிப்போ சம்மதமோ இல்லாமல் கூடுதலாக 3 சதவீதம் அபராத வட்டி கொடுக்கவும், இந்த கடன் தொகையை [உரிய முறையில்] வசூல் செய்து, எனது நகையை விற்பனை செய்து கொள்ளவும் நிறுவனத்திற்கு முழு அதிகாரம் உண்டு. இதனால் எனக்கு எந்த ஆட்சேபனையும் இல்லை."));
        stack.Children.Add(pDeed);

        var pNom = new TextBlock
        {
            FontSize = 9.5,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 1, 0, 6)
        };
        pNom.Inlines.Add(new Run("என்னுடைய பிற்கால அவகாசியான திரு/திருமதி "));
        pNom.Inlines.Add(new Bold(new Run(nominee)));
        pNom.Inlines.Add(new Run(" முதலும் வட்டியும் வாங்கிக் கொண்டு அடமான நகையை கொடுத்து விடலாமென்று நான் உறுதி கூறுகிறேன்."));
        stack.Children.Add(pNom);

        // ── 5. Two Signature / Return Receipt Boxes (Tables 1 & 2 in Word) ──
        var sigGrid = new Grid { Margin = new Thickness(0, 4, 0, 6) };
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

        var box1 = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(1), Padding = new Thickness(6, 4, 6, 4), Height = 65 };
        var b1Stack = new DockPanel();
        b1Stack.Children.Add(new TextBlock
        {
            Text = "முதலும், வட்டியும் செலுத்தி பின்பக்கம் குறிப்பிட்டிருக்கிற நகையை சரிபார்த்துத் திரும்பப் பெற்றுக் கொண்டேன்.",
            FontSize = 8.5,
            LineHeight = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack
        });
        var b1Btm = new DockPanel();
        DockPanel.SetDock(b1Btm, Dock.Bottom);
        b1Btm.Children.Add(new TextBlock { Text = "தேதி : ____________", FontSize = 9, Foreground = InkBlack });
        var b1Sig = new TextBlock { Text = "கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9, Foreground = InkBlack };
        DockPanel.SetDock(b1Sig, Dock.Right);
        b1Btm.Children.Add(b1Sig);
        b1Stack.Children.Add(b1Btm);
        box1.Child = b1Stack;
        Grid.SetColumn(box1, 0);
        sigGrid.Children.Add(box1);

        var box2 = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(1), Padding = new Thickness(6, 4, 6, 4), Height = 65 };
        var b2Stack = new DockPanel();
        var b2Btm = new DockPanel();
        DockPanel.SetDock(b2Btm, Dock.Bottom);
        b2Btm.Children.Add(new TextBlock { Text = "தேதி : ____________", FontSize = 9, Foreground = InkBlack });
        var b2Sig = new TextBlock { Text = "கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9, Foreground = InkBlack };
        DockPanel.SetDock(b2Sig, Dock.Right);
        b2Btm.Children.Add(b2Sig);
        b2Stack.Children.Add(b2Btm);
        box2.Child = b2Stack;
        Grid.SetColumn(box2, 2);
        sigGrid.Children.Add(box2);

        stack.Children.Add(sigGrid);

        // ── 6. Repayment Installment Table (Table 3 in Word) ──
        var repBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 0, 6)
        };
        var repStack = new StackPanel();
        repBorder.Child = repStack;

        var repTh = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(4, 3, 4, 3) };
        var thG = new Grid();
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        thG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void AddThR(int col, string t, TextAlignment a = TextAlignment.Left)
        {
            var tb = new TextBlock { Text = t, FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = a, Foreground = InkBlack };
            Grid.SetColumn(tb, col); thG.Children.Add(tb);
        }
        AddThR(0, "தேதி");
        AddThR(1, "வட்டி வரவு", TextAlignment.Right);
        AddThR(2, "அசல் வரவு", TextAlignment.Right);
        AddThR(3, "அசல் பாக்கி", TextAlignment.Right);
        AddThR(4, "கையொப்பம்", TextAlignment.Center);
        AddThR(5, "கொண்டு வந்த தேதி");
        repTh.Child = thG;
        repStack.Children.Add(repTh);

        int lines = 0;
        decimal bal = loan.LoanAmount;
        if (pastReceipts != null)
        {
            foreach (var r in pastReceipts.OrderBy(x => x.ReceiptDate).Take(3))
            {
                bal -= r.PrincipalAmount;
                var row = new Grid { Margin = new Thickness(4, 2, 4, 2) };
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

        for (int i = lines; i < 3; i++)
        {
            var blank = new Border
            {
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x00, 0x00, 0x00)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 20
            };
            repStack.Children.Add(blank);
        }
        stack.Children.Add(repBorder);

        // ── 7. Conditions (நிபந்தனைகள்) ──
        stack.Children.Add(new TextBlock
        {
            Text = "நிபந்தனைகள் :",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Foreground = InkBlack,
            Margin = new Thickness(0, 3, 0, 2)
        });

        void AddRule(string r) => stack.Children.Add(new TextBlock
        {
            Text = r,
            FontSize = 8.5,
            LineHeight = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 1, 0, 1)
        });

        AddRule("1.  அடகு வைப்பவர் தனக்கு சொந்தமில்லாத நகையை அடகு வைக்க கூடாது.");
        AddRule("2.  அடகு சீட்டினை தவறவிட்டால் ரூ.50 பத்திரத்தில் உறுதிமொழி எழுதிக் கொடுத்து அடகு வைத்த நகையை திரும்பப் பெற்றுக் கொள்ளலாம்.");
        AddRule("3.  விண்ணப்பத்தில் தரப்பட்டுள்ள விலாசம் மற்றும் தொலைபேசி எண் இவற்றில் மாற்றம் ஏற்பட்டால் நிறுவனத்துக்கு நேரிலோ அல்லது ரிஜிஸ்டர் போஸ்ட் மூலமாகவோ தெரிவிக்க வேண்டும். இல்லையென்றால் விண்ணப்பத்தில் கொடுக்கப்பட்ட விலாசத்திற்கு [அனுப்பப்படும் தகவல்கள் உரியவருக்கு சென்றதாகக் கருதப்படும்].");
        AddRule("4.  1 வருடத்தில் நகையை திரும்ப பெறாவிட்டால் ஏலம் செய்து பணத்தை சரிக்கட்டுவதாகும்.");

        var finalSign = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        finalSign.Children.Add(new TextBlock { Text = "நகையை நல்ல முறையில் பெற்றுக்கொண்டேன்.", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = InkBlack });
        var sigR = new TextBlock { Text = "கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        DockPanel.SetDock(sigR, Dock.Right);
        finalSign.Children.Add(sigR);
        stack.Children.Add(finalSign);

        return container;
    }

    /// <summary>
    /// Sheet 2: Office Jewel Record + Perforation + Customer Receipt Slip (ஈடு வைத்த நகைக்கு ரசீது) — Matches Page 2 of Word Document
    /// </summary>
    public static FrameworkElement CreateInventoryAndSlipVisual(Loan loan, Customer customer, Scheme scheme)
    {
        var items = loan.Items?.ToList() ?? new List<LoanItem>();
        var totalQty = items.Count > 0 ? items.Sum(i => i.Quantity) : 1;
        var totalGross = items.Count > 0 ? items.Sum(i => i.GrossWeight) : loan.GrossWeight;
        var totalNet = items.Count > 0 ? items.Sum(i => i.NetWeight) : loan.NetWeight;
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "________________" : loan.NomineeName;

        var container = new Border
        {
            Width = A4Width,
            Height = A4Height,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(36, 24, 36, 24)
        };
        TextElement.SetFontFamily(container, DefaultFont);

        var stack = new StackPanel();
        container.Child = stack;

        // ══════════════════════════════════════════════════════════════════════
        // TOP HALF: Office Copy (நகைப் பட்டியல் & உறுதிமொழி)
        // ══════════════════════════════════════════════════════════════════════
        stack.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 6)
        });

        // Table 4: Loan No, Amount, Loan Date, Customer ID, Address, Scheme
        var t4Border = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var t4Grid = new Grid();
        t4Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        t4Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 3; i++) t4Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddT4Row(int r, int c, string lbl, string val, bool bold = false)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1.5, 4, 1.5) };
            p.Children.Add(new TextBlock { Text = lbl.PadRight(12), FontWeight = FontWeights.SemiBold, FontSize = 9.5, Foreground = InkBlack });
            p.Children.Add(new TextBlock { Text = ": " + val, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = 9.5, Foreground = InkBlack });
            Grid.SetRow(p, r); Grid.SetColumn(p, c); t4Grid.Children.Add(p);
        }

        AddT4Row(0, 0, "Loan No", loan.LoanNumber, true);
        AddT4Row(0, 1, "Amount", $"₹ {loan.LoanAmount:N2} /-", true);
        AddT4Row(1, 0, "Loan Date", loan.PledgeDate.ToString("dd-MM-yyyy"));
        AddT4Row(1, 1, "Customer ID", customer.Code, true);
        AddT4Row(2, 0, "Address", $"{customer.Name}, {customer.Address}");
        AddT4Row(2, 1, "Scheme", scheme.Name);
        t4Border.Child = t4Grid;
        stack.Children.Add(t4Border);

        // Table 5: Jewel Name | JType | Qty | G.Weight | N.Weight
        var t5Border = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var t5Stack = new StackPanel();
        t5Border.Child = t5Stack;

        var t5Th = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(4, 3, 4, 3) };
        var t5Grid = new Grid();
        t5Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Jewel Name
        t5Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // JType
        t5Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // Qty
        t5Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // G.Weight
        t5Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // N.Weight

        void AddT5Th(int c, string t, TextAlignment a = TextAlignment.Left)
        {
            var tb = new TextBlock { Text = t, FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = a, Foreground = InkBlack };
            Grid.SetColumn(tb, c); t5Grid.Children.Add(tb);
        }
        AddT5Th(0, "Jewel Name");
        AddT5Th(1, "JType");
        AddT5Th(2, "Qty", TextAlignment.Center);
        AddT5Th(3, "G.Weight", TextAlignment.Right);
        AddT5Th(4, "N.Weight", TextAlignment.Right);
        t5Th.Child = t5Grid;
        t5Stack.Children.Add(t5Th);

        int cCount = 0;
        foreach (var it in items)
        {
            var row = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            for (int c = 0; c < 5; c++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = t5Grid.ColumnDefinitions[c].Width });

            var jt = it.JewelType?.Name ?? "Gold";
            var desc = string.IsNullOrWhiteSpace(it.Description) ? jt : it.Description;

            void AddC(int col, string v, TextAlignment a = TextAlignment.Left)
            {
                var tb = new TextBlock { Text = v, FontSize = 9, TextAlignment = a, Foreground = InkBlack };
                Grid.SetColumn(tb, col); row.Children.Add(tb);
            }
            AddC(0, desc);
            AddC(1, jt);
            AddC(2, it.Quantity.ToString(), TextAlignment.Center);
            AddC(3, $"{it.GrossWeight:F3}", TextAlignment.Right);
            AddC(4, $"{it.NetWeight:F3}", TextAlignment.Right);
            t5Stack.Children.Add(row);
            cCount++;
            if (cCount >= 4) break;
        }

        // Table 5 Totals Row
        var t5Tf = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(4, 3, 4, 3) };
        var tfG = new Grid();
        for (int c = 0; c < 5; c++) tfG.ColumnDefinitions.Add(new ColumnDefinition { Width = t5Grid.ColumnDefinitions[c].Width });

        var tfQty = new TextBlock { Text = $"Total Qty : {totalQty}", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        Grid.SetColumn(tfQty, 0); tfG.Children.Add(tfQty);

        var tfGw = new TextBlock { Text = $"Gross Weight : {totalGross:F3}", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(tfGw, 3); tfG.Children.Add(tfGw);

        var tfNw = new TextBlock { Text = $"Net Weight : {totalNet:F3}", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(tfNw, 4); tfG.Children.Add(tfNw);
        t5Tf.Child = tfG;
        t5Stack.Children.Add(t5Tf);
        stack.Children.Add(t5Border);

        // Office Declaration (Exact from Word Document)
        stack.Children.Add(new TextBlock
        {
            Text = $"மேலே விவரிக்கும் அடமானப் பொருட்கள் முற்றிலும் எனக்குச் சொந்தமானவை. அதன்மேல் வேறு எவருக்கும் அவகாசமில்லை. மேற்படி நகைகள் சுத்தமான தங்க நகைகள் என்றும், எனது கணவர்/மனைவி/தாயார்/மகன்/மகள் திரு/திருமதி {nominee} [உண்மையான உரிமையாளர் என்றும்] முதலும் வட்டியும் செலுத்தி [நகையை] மீட்டுக் கொள்வேன் என்றும் உறுதி கூறுகிறேன்.",
            FontSize = 8.5,
            LineHeight = 13.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 2, 0, 8)
        });

        // Signatures
        var offSig = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        offSig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        offSig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sBor = new TextBlock { Text = "கடன்தாரர் கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        var sMgr = new TextBlock { Text = "Manager / Director", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(sBor, 0); offSig.Children.Add(sBor);
        Grid.SetColumn(sMgr, 1); offSig.Children.Add(sMgr);
        stack.Children.Add(offSig);

        // ══════════════════════════════════════════════════════════════════════
        // PERFORATION CUT LINE (Exact from Word Document)
        // ══════════════════════════════════════════════════════════════════════
        stack.Children.Add(new TextBlock
        {
            Text = "✂ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -",
            FontSize = 10,
            Foreground = LightDividerBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 10)
        });

        // ══════════════════════════════════════════════════════════════════════
        // BOTTOM HALF: Customer Counterfoil Slip (ஈடு வைத்த நகைக்கு ரசீது)
        // ══════════════════════════════════════════════════════════════════════
        stack.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 2)
        });

        var rTitleDock = new DockPanel { Margin = new Thickness(0, 2, 0, 4) };
        rTitleDock.Children.Add(new TextBlock { Text = FirmPhone, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkBlack });
        var tBox = new TextBlock { Text = "ஈடு வைத்த நகைக்கு ரசீது", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = InkBlack, HorizontalAlignment = HorizontalAlignment.Center };
        DockPanel.SetDock(tBox, Dock.Left);
        rTitleDock.Children.Add(tBox);
        stack.Children.Add(rTitleDock);

        // Table 7: Customer Slip Details
        var t7Border = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var t7Grid = new Grid();
        t7Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        t7Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 5; i++) t7Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddT7Row(int r, int c, string lbl, string val, bool bold = false)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1.5, 4, 1.5) };
            p.Children.Add(new TextBlock { Text = lbl.PadRight(12), FontWeight = FontWeights.SemiBold, FontSize = 9.5, Foreground = InkBlack });
            p.Children.Add(new TextBlock { Text = ": " + val, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = 9.5, Foreground = InkBlack });
            Grid.SetRow(p, r); Grid.SetColumn(p, c); t7Grid.Children.Add(p);
        }

        AddT7Row(0, 0, "Customer ID", customer.Code, true);
        AddT7Row(0, 1, "Scheme", scheme.Name);
        AddT7Row(1, 0, "Loan No", loan.LoanNumber, true);
        AddT7Row(1, 1, "Amount", $"₹ {loan.LoanAmount:N2} /-", true);
        AddT7Row(2, 0, "Loan Date", loan.PledgeDate.ToString("dd-MM-yyyy"));
        AddT7Row(2, 1, "Jewel Details", $"Total Qty: {totalQty}, Net Wt: {totalNet:F3} g", true);
        AddT7Row(3, 0, "Address", $"{customer.Address}  Ph: {customer.Phone}");
        AddT7Row(3, 1, "Total Qty", $"{totalQty}   Net Wt: {totalNet:F3} g");
        AddT7Row(4, 0, "திருப்ப வேண்டிய கடைசி தேதி", loan.MaturityDate.ToString("dd-MM-yyyy"), true);
        t7Border.Child = t7Grid;
        stack.Children.Add(t7Border);

        // Timings & Sign-off (Exact from Word Document)
        stack.Children.Add(new TextBlock { Text = "*  அடகு வைக்கும் நேரம்: காலை 9.30 மணி முதல் மாலை 5.30 மணி வரை", FontSize = 8.5, Foreground = InkBlack });
        stack.Children.Add(new TextBlock { Text = "*  நகை திருப்பும் நேரம்: காலை 9.30 மணி முதல் மாலை 4 மணி வரை", FontSize = 8.5, Foreground = InkBlack });
        stack.Children.Add(new TextBlock { Text = "*  சனிக்கிழமை நேரம்: காலை 9.30 மணி முதல் மதியம் 12.30 மணி வரை", FontSize = 8.5, Foreground = InkBlack });

        var rSignOff = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        rSignOff.Children.Add(new TextBlock { Text = "*  ஞாயிறு விடுமுறை", FontWeight = FontWeights.Bold, FontSize = 9, Foreground = InkBlack });
        var forJgf = new TextBlock { Text = $"For {FirmName}", FontWeight = FontWeights.Bold, FontSize = 10, Foreground = InkBlack };
        DockPanel.SetDock(forJgf, Dock.Right);
        rSignOff.Children.Add(forJgf);
        stack.Children.Add(rSignOff);

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
    /// Generates high-resolution PDF document containing Page 1 (Deed) and Page 2 (Inventory & Slip).
    /// Both pages are standard A4 Portrait.
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

            var visual1 = CreateMortgageDeedVisual(loan, customer, scheme, loan.Receipts?.ToList());
            var visual2 = CreateInventoryAndSlipVisual(loan, customer, scheme);

            using var doc = new PdfDocument();
            doc.Info.Title = $"JEBA GOLD FINANCE Pawn Ticket #{loan.LoanNumber}";
            doc.Info.Author = FirmName;
            doc.Info.Subject = "Tamil Nadu Jewel Loan Pawn Ticket & Mortgage Deed";

            // Render Sheet 1 & 2 in standard A4 Portrait (595 x 842 pt)
            RenderVisualToPdfPage(doc, visual1);
            RenderVisualToPdfPage(doc, visual2);

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

    private static void RenderVisualToPdfPage(PdfDocument doc, FrameworkElement visual)
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
        page.Width = XUnit.FromPoint(595);  // A4 Portrait width
        page.Height = XUnit.FromPoint(842); // A4 Portrait height

        using var gfx = XGraphics.FromPdfPage(page);
        using var xImg = XImage.FromStream(ms);
        gfx.DrawImage(xImg, 0, 0, page.Width.Point, page.Height.Point);
    }
}
