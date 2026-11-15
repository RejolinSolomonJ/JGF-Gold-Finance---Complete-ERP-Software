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
/// Generates authentic Tamil Nadu Pawn Ticket & Mortgage Deed documents matching exact physical PDF stationery layout.
/// Page 1: நகை அடமானக் கடன் பத்திரம் (Mortgage Deed & Repayment Ledger & Conditions) with JGF Gold Logo
/// Page 2: Jewel Inventory Record (Office Copy) + Perforation + ஈடு வைத்த நகைக்கு ரசீது (Customer Slip) with JGF Gold Logo
/// </summary>
public static class LoanPawnTicketPrintHelper
{
    private static readonly System.Windows.Media.FontFamily DefaultFont = new("Segoe UI, Nirmala UI, Noto Sans Tamil, Arial");
    private static readonly SolidColorBrush InkBlack = System.Windows.Media.Brushes.Black;
    private static readonly SolidColorBrush BrandRed = new(System.Windows.Media.Color.FromRgb(0x99, 0x1B, 0x1B)); // Official Header Red from PDF
    private static readonly SolidColorBrush BorderPenBrush = System.Windows.Media.Brushes.Black;
    private static readonly SolidColorBrush LightDividerBrush = new(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));

    public const string FirmName = "JEBA GOLD FINANCE";
    public const string FirmType = "(Pawn Brokers)";
    public const string FirmAddress = "South Thamaraikulam, Kanyakumari .";
    public const string FirmPhone = "Cell : 9360557680";

    // Standard A4 Portrait dimensions at 96 DPI: 794 x 1123
    public const double A4Width = 794;
    public const double A4Height = 1123;

    private static ImageSource? _cachedLogo;

    private static ImageSource? GetLogoSource()
    {
        if (_cachedLogo != null) return _cachedLogo;
        try
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png"),
                System.IO.Path.Combine(Environment.CurrentDirectory, "Assets", "logo.png"),
                @"c:\Users\Rejolin Solomon J\Downloads\JGF Finance Software\src\JGF.Desktop\Assets\logo.png"
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(p, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _cachedLogo = bmp;
                    return _cachedLogo;
                }
            }
        }
        catch { }
        return null;
    }

    private static Image CreateLogoImage(double width = 64, double height = 64)
    {
        var img = new Image
        {
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top
        };
        var src = GetLogoSource();
        if (src != null) img.Source = src;
        return img;
    }

    /// <summary>
    /// Sheet 1: நகை அடமானக் கடன் பத்திரம் (Matches Page 1 of user's PDF with Gold Logo)
    /// </summary>
    public static FrameworkElement CreateMortgageDeedVisual(Loan loan, Customer customer, Scheme scheme, List<Receipt>? pastReceipts = null)
    {
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "________________________" : loan.NomineeName;
        var guardian = string.IsNullOrWhiteSpace(customer.FatherOrHusbandName) ? "________________________" : customer.FatherOrHusbandName;
        var wordsAmount = AmountToWordsHelper.ToIndianRupees(loan.LoanAmount);

        var outerContainer = new Border
        {
            Width = A4Width,
            Height = A4Height,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 20, 24, 20)
        };
        TextElement.SetFontFamily(outerContainer, DefaultFont);

        var mainStack = new StackPanel();
        outerContainer.Child = mainStack;

        // ── 1. Top Header with JGF Logo ──
        var topHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        topHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        topHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logoImg = CreateLogoImage(62, 62);
        Grid.SetColumn(logoImg, 0);
        topHeaderGrid.Children.Add(logoImg);

        var titleStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-70, 0, 0, 0) };
        titleStack.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 21,
            Foreground = BrandRed,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = FirmType,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 1)
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10.5,
            FontWeight = FontWeights.Normal,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3)
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "நகை அடமானக் கடன் பத்திரம்",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            TextDecorations = TextDecorations.Underline,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        Grid.SetColumn(titleStack, 1);
        topHeaderGrid.Children.Add(titleStack);
        mainStack.Children.Add(topHeaderGrid);

        // ── 2. Top Table: G.L.No | தேதி | கடன் தொகை ரூபாய் ──
        var topBox = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 0, 6)
        };
        var tg = new Grid();
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var glBorder = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(8, 4, 8, 4), Height = 48 };
        glBorder.Child = new TextBlock { Text = $"G.L.No : {loan.LoanNumber}", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = InkBlack, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(glBorder, 0); tg.Children.Add(glBorder);

        var dtBorder = new Border { Padding = new Thickness(10, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center };
        dtBorder.Child = new TextBlock { Text = $"தேதி : {loan.PledgeDate:dd-MM-yyyy}", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = InkBlack };
        Grid.SetColumn(dtBorder, 1); tg.Children.Add(dtBorder);

        var amtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 10, 0) };
        amtStack.Children.Add(new TextBlock { Text = "கடன் தொகை ரூபாய் :", FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = InkBlack, TextAlignment = TextAlignment.Right });
        amtStack.Children.Add(new TextBlock { Text = $"₹ {loan.LoanAmount:N2} /-", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = InkBlack, TextAlignment = TextAlignment.Right });
        Grid.SetColumn(amtStack, 2); tg.Children.Add(amtStack);

        topBox.Child = tg;
        mainStack.Children.Add(topBox);

        // ── 3. Customer & Guardian Info Lines ──
        var pInfo1 = new TextBlock { FontSize = 10, LineHeight = 16, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 2) };
        pInfo1.Inlines.Add(new Run("உறுப்பினர் எண்: "));
        pInfo1.Inlines.Add(new Bold(new Run(customer.Code)));
        pInfo1.Inlines.Add(new Run("                ஊர்: "));
        pInfo1.Inlines.Add(new Bold(new Run(customer.Address)));
        mainStack.Children.Add(pInfo1);

        var pInfo2 = new TextBlock { FontSize = 10, LineHeight = 16, Foreground = InkBlack, Margin = new Thickness(0, 0, 0, 4) };
        pInfo2.Inlines.Add(new Run("திரு/திருமதி: "));
        pInfo2.Inlines.Add(new Bold(new Run(customer.Name)));
        pInfo2.Inlines.Add(new Run("   மகன்/மகள்/கணவர்/மனைவி: "));
        pInfo2.Inlines.Add(new Bold(new Run(guardian)));
        mainStack.Children.Add(pInfo2);

        // ── 4. Legal Deed Tamil Body Text (Exact from PDF) ──
        mainStack.Children.Add(new TextBlock
        {
            Text = "எழுதிக் கொடுத்த நகை அடமான கடன் பத்திரமாவது.",
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            TextDecorations = TextDecorations.Underline,
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
        mainStack.Children.Add(pDeed);

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
        mainStack.Children.Add(pNom);

        // ── 5. Signature / Return Verification Box (Exact from PDF) ──
        var sigBorder = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            Height = 75,
            Margin = new Thickness(0, 2, 0, 6)
        };
        var sigDock = new DockPanel();
        sigDock.Children.Add(new TextBlock
        {
            Text = "முதலும், வட்டியும் செலுத்தி பின்பக்கம் குறிப்பிட்டிருக்கிற நகையை சரிபார்த்துத் திரும்பப் பெற்றுக் கொண்டேன்.",
            FontSize = 9,
            LineHeight = 13.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack
        });
        var sigBtm = new DockPanel();
        DockPanel.SetDock(sigBtm, Dock.Bottom);
        sigBtm.Children.Add(new TextBlock { Text = "தேதி : ____________", FontSize = 9.5, Foreground = InkBlack });
        var sigLbl = new TextBlock { Text = "கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        DockPanel.SetDock(sigLbl, Dock.Right);
        sigBtm.Children.Add(sigLbl);
        sigDock.Children.Add(sigBtm);
        sigBorder.Child = sigDock;
        mainStack.Children.Add(sigBorder);

        // ── 6. Repayment Installment Table (Exact from PDF) ──
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
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0x00, 0x00, 0x00)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 22
            };
            repStack.Children.Add(blank);
        }
        mainStack.Children.Add(repBorder);

        // ── 7. Conditions (நிபந்தனைகள் - Exact from PDF) ──
        mainStack.Children.Add(new TextBlock
        {
            Text = "நிபந்தனைகள் :",
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            Foreground = InkBlack,
            Margin = new Thickness(0, 2, 0, 2)
        });

        void AddRule(string r) => mainStack.Children.Add(new TextBlock
        {
            Text = r,
            FontSize = 8.5,
            LineHeight = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 1, 0, 1)
        });

        AddRule("1. அடகு வைப்பவர் தனக்கு சொந்தமில்லாத நகையை அடகு வைக்க கூடாது.");
        AddRule("2. அடகு சீட்டினை தவறவிட்டால் ரூ.50 பத்திரத்தில் உறுதிமொழி எழுதிக் கொடுத்து அடகு வைத்த நகையை திரும்பப் பெற்றுக் கொள்ளலாம்.");
        AddRule("3. விண்ணப்பத்தில் தரப்பட்டுள்ள விலாசம் மற்றும் தொலைபேசி எண் இவற்றில் மாற்றம் ஏற்பட்டால் நிறுவனத்துக்கு நேரிலோ அல்லது ரிஜிஸ்டர் போஸ்ட் மூலமாகவோ தெரிவிக்க வேண்டும். இல்லையென்றால் விண்ணப்பத்தில் கொடுக்கப்பட்ட விலாசத்திற்கு [அனுப்பப்படும் தகவல்கள் உரியவருக்கு சென்றதாகக் கருதப்படும்].");
        AddRule("4. 1 வருடத்தில் நகையை திரும்ப பெறாவிட்டால் ஏலம் செய்து பணத்தை சரிக்கட்டுவதாகும்.");

        var finalSign = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        finalSign.Children.Add(new TextBlock { Text = "நகையை நல்ல முறையில் பெற்றுக்கொண்டேன்.", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = InkBlack });
        var sigR = new TextBlock { Text = "கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        DockPanel.SetDock(sigR, Dock.Right);
        finalSign.Children.Add(sigR);
        mainStack.Children.Add(finalSign);

        return outerContainer;
    }

    /// <summary>
    /// Sheet 2: Office Jewel Record + Perforation + Customer Receipt Slip (Matches Page 2 of user's PDF with Gold Logo)
    /// </summary>
    public static FrameworkElement CreateInventoryAndSlipVisual(Loan loan, Customer customer, Scheme scheme)
    {
        var items = loan.Items?.ToList() ?? new List<LoanItem>();
        var totalQty = items.Count > 0 ? items.Sum(i => i.Quantity) : 1;
        var totalGross = items.Count > 0 ? items.Sum(i => i.GrossWeight) : loan.GrossWeight;
        var totalNet = items.Count > 0 ? items.Sum(i => i.NetWeight) : loan.NetWeight;
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "________________" : loan.NomineeName;

        var outerContainer = new Border
        {
            Width = A4Width,
            Height = A4Height,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 18, 24, 18)
        };
        TextElement.SetFontFamily(outerContainer, DefaultFont);

        var mainStack = new StackPanel();
        outerContainer.Child = mainStack;

        // ══════════════════════════════════════════════════════════════════════
        // TOP HALF: Office Copy (நகைப் பட்டியல் & உறுதிமொழி) with JGF Logo
        // ══════════════════════════════════════════════════════════════════════
        var offHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        offHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        offHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logoImg1 = CreateLogoImage(58, 58);
        Grid.SetColumn(logoImg1, 0);
        offHeaderGrid.Children.Add(logoImg1);

        var offTitleStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-70, 0, 0, 0) };
        offTitleStack.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 20,
            Foreground = BrandRed,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        offTitleStack.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10.5,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 2)
        });
        Grid.SetColumn(offTitleStack, 1);
        offHeaderGrid.Children.Add(offTitleStack);
        mainStack.Children.Add(offHeaderGrid);

        // Table 1 (Office Metadata Table with black borders):
        var t1Border = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 0, 6)
        };
        var t1Grid = new Grid();
        t1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        t1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        t1Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        t1Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        t1Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Border CreateCell(int r, int c, string lbl, string val, bool bold = false, bool bottomLine = true)
        {
            var b = new Border
            {
                BorderBrush = BorderPenBrush,
                BorderThickness = new Thickness(c == 0 ? 0 : 1, 0, 0, bottomLine ? 1 : 0),
                Padding = new Thickness(8, 4, 8, 4)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = lbl.PadRight(12), FontWeight = FontWeights.SemiBold, FontSize = 9.5, Foreground = InkBlack });
            sp.Children.Add(new TextBlock { Text = ": " + val, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = 9.5, Foreground = InkBlack });
            b.Child = sp;
            Grid.SetRow(b, r); Grid.SetColumn(b, c);
            t1Grid.Children.Add(b);
            return b;
        }

        CreateCell(0, 0, "Loan No", loan.LoanNumber, true);
        CreateCell(0, 1, "Amount", $"₹ {loan.LoanAmount:N2} /-", true);
        CreateCell(1, 0, "Loan Date", loan.PledgeDate.ToString("dd-MM-yyyy"));
        CreateCell(1, 1, "Customer ID", customer.Code, true);

        var r3Cell = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(8, 4, 8, 4)
        };
        var r3Stack = new StackPanel();
        r3Stack.Children.Add(new TextBlock { Text = $"Address : {customer.Name}, {customer.Address}", FontSize = 9.5, Foreground = InkBlack });
        r3Stack.Children.Add(new TextBlock { Text = $"Scheme : {scheme.Name}", FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkBlack, Margin = new Thickness(0, 2, 0, 0) });
        r3Cell.Child = r3Stack;
        Grid.SetRow(r3Cell, 2); Grid.SetColumnSpan(r3Cell, 2);
        t1Grid.Children.Add(r3Cell);

        t1Border.Child = t1Grid;
        mainStack.Children.Add(t1Border);

        // Table 2: Jewel Items Table
        var t2Border = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var t2Stack = new StackPanel();
        t2Border.Child = t2Stack;

        var t2Th = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(6, 4, 6, 4) };
        var t2Grid = new Grid();
        t2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Jewel Name
        t2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // JType
        t2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // Qty
        t2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // G.Weight
        t2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // N.Weight

        void AddT2Th(int col, string text, TextAlignment align = TextAlignment.Left)
        {
            var tb = new TextBlock { Text = text, FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = align, Foreground = InkBlack };
            Grid.SetColumn(tb, col); t2Grid.Children.Add(tb);
        }
        AddT2Th(0, "Jewel Name");
        AddT2Th(1, "JType");
        AddT2Th(2, "Qty", TextAlignment.Center);
        AddT2Th(3, "G.Weight", TextAlignment.Right);
        AddT2Th(4, "N.Weight", TextAlignment.Right);
        t2Th.Child = t2Grid;
        t2Stack.Children.Add(t2Th);

        int count = 0;
        foreach (var it in items)
        {
            var row = new Grid { Margin = new Thickness(6, 2.5, 6, 2.5) };
            for (int c = 0; c < 5; c++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = t2Grid.ColumnDefinitions[c].Width });

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
            t2Stack.Children.Add(row);
            count++;
            if (count >= 4) break;
        }

        // Totals Footer Row
        var t2Tf = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(6, 4, 6, 4) };
        var tfG = new Grid();
        for (int c = 0; c < 5; c++) tfG.ColumnDefinitions.Add(new ColumnDefinition { Width = t2Grid.ColumnDefinitions[c].Width });

        var tfQty = new TextBlock { Text = $"Total Qty : {totalQty}", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        Grid.SetColumn(tfQty, 0); tfG.Children.Add(tfQty);

        var tfGw = new TextBlock { Text = $"Gross Weight : {totalGross:F3}", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(tfGw, 3); tfG.Children.Add(tfGw);

        var tfNw = new TextBlock { Text = $"Net Weight : {totalNet:F3}", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(tfNw, 4); tfG.Children.Add(tfNw);
        t2Tf.Child = tfG;
        t2Stack.Children.Add(t2Tf);
        mainStack.Children.Add(t2Border);

        // Office Declaration (Exact from PDF)
        mainStack.Children.Add(new TextBlock
        {
            Text = $"மேலே விவரிக்கும் அடமானப் பொருட்கள் முற்றிலும் எனக்குச் சொந்தமானவை. அதன்மேல் வேறு எவருக்கும் அவகாசமில்லை. மேற்படி நகைகள் சுத்தமான தங்க நகைகள் என்றும், எனது கணவர்/மனைவி/தாயார்/மகன்/மகள் திரு/திருமதி {nominee} [உண்மையான உரிமையாளர் என்றும்] முதலும் வட்டியும் செலுத்தி [நகையை] மீட்டுக் கொள்வேன் என்றும் உறுதி கூறுகிறேன்.",
            FontSize = 8.5,
            LineHeight = 13.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = InkBlack,
            Margin = new Thickness(0, 1, 0, 6)
        });

        // Signatures
        var offSig = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        offSig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        offSig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sBor = new TextBlock { Text = "கடன்தாரர் கையொப்பம்", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = InkBlack };
        var sMgr = new TextBlock { Text = "Manager / Director", FontWeight = FontWeights.Bold, FontSize = 9.5, TextAlignment = TextAlignment.Right, Foreground = InkBlack };
        Grid.SetColumn(sBor, 0); offSig.Children.Add(sBor);
        Grid.SetColumn(sMgr, 1); offSig.Children.Add(sMgr);
        mainStack.Children.Add(offSig);

        // ══════════════════════════════════════════════════════════════════════
        // PERFORATION CUT LINE (Exact from PDF)
        // ══════════════════════════════════════════════════════════════════════
        mainStack.Children.Add(new TextBlock
        {
            Text = "✂ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -",
            FontSize = 10,
            Foreground = LightDividerBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 6)
        });

        // ══════════════════════════════════════════════════════════════════════
        // BOTTOM HALF: Customer Counterfoil Slip (ஈடு வைத்த நகைக்கு ரசீது)
        // ══════════════════════════════════════════════════════════════════════
        mainStack.Children.Add(new TextBlock
        {
            Text = FirmName,
            FontWeight = FontWeights.Bold,
            FontSize = 19,
            Foreground = BrandRed,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        mainStack.Children.Add(new TextBlock
        {
            Text = FirmAddress,
            FontSize = 10,
            Foreground = InkBlack,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 1)
        });

        var rTitleDock = new DockPanel { Margin = new Thickness(0, 1, 0, 4) };
        var tBox = new TextBlock { Text = "ஈடு வைத்த நகைக்கு ரசீது", FontWeight = FontWeights.Bold, FontSize = 12.5, Foreground = InkBlack, HorizontalAlignment = HorizontalAlignment.Center };
        var tCell = new TextBlock { Text = FirmPhone, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = InkBlack };
        DockPanel.SetDock(tCell, Dock.Right);
        rTitleDock.Children.Add(tCell);
        rTitleDock.Children.Add(tBox);
        mainStack.Children.Add(rTitleDock);

        // Table 3: Customer Slip Details (Exact from PDF)
        var t3Border = new Border
        {
            BorderBrush = BorderPenBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var t3Grid = new Grid();
        t3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        t3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 5; i++) t3Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Border AddT3Cell(int r, int c, string lbl, string val, bool bold = false, bool bottomLine = true)
        {
            var b = new Border
            {
                BorderBrush = BorderPenBrush,
                BorderThickness = new Thickness(c == 0 ? 0 : 1, 0, 0, bottomLine ? 1 : 0),
                Padding = new Thickness(8, 3.5, 8, 3.5)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = lbl.PadRight(12), FontWeight = FontWeights.SemiBold, FontSize = 9.5, Foreground = InkBlack });
            sp.Children.Add(new TextBlock { Text = ": " + val, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = 9.5, Foreground = InkBlack });
            b.Child = sp;
            Grid.SetRow(b, r); Grid.SetColumn(b, c);
            t3Grid.Children.Add(b);
            return b;
        }

        AddT3Cell(0, 0, "Customer ID", customer.Code, true);
        AddT3Cell(0, 1, "Scheme", scheme.Name);
        AddT3Cell(1, 0, "Loan No", loan.LoanNumber, true);
        AddT3Cell(1, 1, "Amount", $"₹ {loan.LoanAmount:N2} /-", true);
        AddT3Cell(2, 0, "Loan Date", loan.PledgeDate.ToString("dd-MM-yyyy"));
        AddT3Cell(2, 1, "Jewel Details", $"Total Qty: {totalQty}, Net Wt: {totalNet:F3} g", true);

        // Row 3: Address & Phone (left), Total Qty & Net Wt (right)
        var r3Left = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(8, 3.5, 8, 3.5) };
        var spAddr = new StackPanel();
        spAddr.Children.Add(new TextBlock { Text = $"Address : {customer.Address}", FontSize = 9.5, Foreground = InkBlack });
        spAddr.Children.Add(new TextBlock { Text = $"Ph : {customer.Phone}", FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = InkBlack });
        r3Left.Child = spAddr;
        Grid.SetRow(r3Left, 3); Grid.SetColumn(r3Left, 0); t3Grid.Children.Add(r3Left);

        var r3Right = new Border { BorderBrush = BorderPenBrush, BorderThickness = new Thickness(1, 0, 0, 1), Padding = new Thickness(8, 3.5, 8, 3.5) };
        var spWt = new StackPanel();
        spWt.Children.Add(new TextBlock { Text = $"Total Qty : {totalQty}", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = InkBlack });
        spWt.Children.Add(new TextBlock { Text = $"Net Weight : {totalNet:F3} g", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = InkBlack });
        r3Right.Child = spWt;
        Grid.SetRow(r3Right, 3); Grid.SetColumn(r3Right, 1); t3Grid.Children.Add(r3Right);

        // Row 4: திருப்ப வேண்டிய கடைசி தேதி (Full width)
        var r4Cell = new Border { Padding = new Thickness(8, 3.5, 8, 3.5) };
        var r4Sp = new StackPanel { Orientation = Orientation.Horizontal };
        r4Sp.Children.Add(new TextBlock { Text = "திருப்ப வேண்டிய கடைசி தேதி : ", FontWeight = FontWeights.Bold, FontSize = 10, Foreground = InkBlack });
        r4Sp.Children.Add(new TextBlock { Text = loan.MaturityDate.ToString("dd-MM-yyyy"), FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = InkBlack });
        r4Cell.Child = r4Sp;
        Grid.SetRow(r4Cell, 4); Grid.SetColumnSpan(r4Cell, 2); t3Grid.Children.Add(r4Cell);

        t3Border.Child = t3Grid;
        mainStack.Children.Add(t3Border);

        // Timings & Sign-off (Exact from PDF)
        mainStack.Children.Add(new TextBlock { Text = "*  அடகு வைக்கும் நேரம்: காலை 9.30 மணி முதல் மாலை 5.30 மணி வரை", FontSize = 8.5, Foreground = InkBlack });
        mainStack.Children.Add(new TextBlock { Text = "*  நகை திருப்பும் நேரம்: காலை 9.30 மணி முதல் மாலை 4 மணி வரை", FontSize = 8.5, Foreground = InkBlack });
        mainStack.Children.Add(new TextBlock { Text = "*  சனிக்கிழமை நேரம்: காலை 9.30 மணி முதல் மதியம் 12.30 மணி வரை", FontSize = 8.5, Foreground = InkBlack });

        var rSignOff = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
        rSignOff.Children.Add(new TextBlock { Text = "*  ஞாயிறு விடுமுறை", FontWeight = FontWeights.Bold, FontSize = 9, Foreground = InkBlack });
        var forJgf = new TextBlock { Text = $"For {FirmName}", FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = InkBlack };
        DockPanel.SetDock(forJgf, Dock.Right);
        rSignOff.Children.Add(forJgf);
        mainStack.Children.Add(rSignOff);

        return outerContainer;
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
    /// Generates high-resolution 2-Page A4 Portrait PDF.
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
