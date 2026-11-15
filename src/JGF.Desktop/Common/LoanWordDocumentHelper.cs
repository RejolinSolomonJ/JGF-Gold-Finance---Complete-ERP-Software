using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JGF.Domain.Entities;
using Microsoft.Win32;
using Serilog;

namespace JGF.Desktop.Common;

/// <summary>
/// Pre-fills the user's authentic Word Document template (JGF_Finance_Jewel_Loan_Forms.docx)
/// with 100% exact alignment, tables, Tamil wording, and formatting preserved.
/// </summary>
public static class LoanWordDocumentHelper
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// Finds the master template docx file on disk.
    /// </summary>
    public static string? FindTemplatePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "JGF_Finance_Jewel_Loan_Forms.docx"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JGF_Finance_Jewel_Loan_Forms.docx"),
            Path.Combine(Environment.CurrentDirectory, "Templates", "JGF_Finance_Jewel_Loan_Forms.docx"),
            Path.Combine(Environment.CurrentDirectory, "JGF_Finance_Jewel_Loan_Forms.docx"),
            Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "", "JGF_Finance_Jewel_Loan_Forms.docx"),
            @"C:\Users\Rejolin Solomon J\Downloads\JGF Finance Software\JGF_Finance_Jewel_Loan_Forms.docx",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "JGF_Finance_Jewel_Loan_Forms.docx"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "JGF Finance Software", "JGF_Finance_Jewel_Loan_Forms.docx")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        return null;
    }

    /// <summary>
    /// Generates a filled Word (.docx) document for the given loan, customer, and scheme.
    /// </summary>
    public static string GenerateFilledDocument(Loan loan, Customer customer, Scheme scheme, string? targetPath = null)
    {
        var templatePath = FindTemplatePath();
        if (templatePath == null || !File.Exists(templatePath))
        {
            throw new FileNotFoundException("Master Word document template 'JGF_Finance_Jewel_Loan_Forms.docx' was not found.");
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "JGF_Tickets");
            Directory.CreateDirectory(tempDir);
            var safeLoanNo = loan.LoanNumber.Replace('/', '_').Replace('\\', '_');
            targetPath = Path.Combine(tempDir, $"JGF_PawnTicket_{safeLoanNo}_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
        }

        // Copy template to target
        File.Copy(templatePath, targetPath, overwrite: true);

        var items = loan.Items?.ToList() ?? new List<LoanItem>();
        var totalQty = items.Count > 0 ? items.Sum(i => i.Quantity) : 1;
        var totalGross = items.Count > 0 ? items.Sum(i => i.GrossWeight) : loan.GrossWeight;
        var totalNet = items.Count > 0 ? items.Sum(i => i.NetWeight) : loan.NetWeight;
        var nominee = string.IsNullOrWhiteSpace(loan.NomineeName) ? "________________" : loan.NomineeName;
        var guardian = string.IsNullOrWhiteSpace(customer.FatherOrHusbandName) ? "________________" : customer.FatherOrHusbandName;
        var wordsAmount = AmountToWordsHelper.ToIndianRupees(loan.LoanAmount);

        // Open docx package and update word/document.xml
        using (var zip = ZipFile.Open(targetPath, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("word/document.xml");
            if (entry != null)
            {
                XDocument xDoc;
                using (var s = entry.Open())
                {
                    xDoc = XDocument.Load(s);
                }

                // Clean proofErr tags that break runs
                xDoc.Descendants(W + "proofErr").Remove();

                // Process paragraphs and replace placeholders
                var paragraphs = xDoc.Descendants(W + "p").ToList();
                foreach (var p in paragraphs)
                {
                    var tNodes = p.Descendants(W + "t").ToList();
                    if (tNodes.Count == 0) continue;

                    var pText = string.Concat(tNodes.Select(t => t.Value));

                    // 1. G.L.No :
                    if (pText.Contains("G.L.No") && !pText.Contains(loan.LoanNumber))
                    {
                        ReplaceRunText(tNodes, "G.L.No :", $"G.L.No : {loan.LoanNumber}");
                        continue;
                    }

                    // 2. தேதி : (in top table or deed)
                    if (pText.StartsWith("தேதி :") || pText.Trim() == "தேதி :")
                    {
                        ReplaceRunText(tNodes, "தேதி :", $"தேதி : {loan.PledgeDate:dd-MM-yyyy}");
                        continue;
                    }

                    // 3. கடன் தொகை ரூபாய் : ………………/-
                    if (pText.Contains("கடன் தொகை ரூபாய் :"))
                    {
                        ReplaceRunText(tNodes, pText, $"கடன் தொகை ரூபாய் : ₹ {loan.LoanAmount:N2} /-");
                        continue;
                    }

                    // 4. உறுப்பினர் எண்: ……………………… ஊர்: ………………………………………………
                    if (pText.Contains("உறுப்பினர் எண்:") && pText.Contains("ஊர்:"))
                    {
                        ReplaceRunText(tNodes, pText, $"உறுப்பினர் எண்: {customer.Code}                ஊர்: {customer.Address}");
                        continue;
                    }

                    // 5. திரு/திருமதி: …………………………………………… மகன்/மகள்/கணவர்/மனைவி: ……………………………………………
                    if (pText.Contains("திரு/திருமதி:") && pText.Contains("மகன்/மகள்/கணவர்/மனைவி:"))
                    {
                        ReplaceRunText(tNodes, pText, $"திரு/திருமதி: {customer.Name}   மகன்/மகள்/கணவர்/மனைவி: {guardian}");
                        continue;
                    }

                    // 6. நான் மேற்படி நிறுவனத்தில் இருந்து...
                    if (pText.Contains("நான் மேற்படி நிறுவனத்தில் இருந்து") && pText.Contains("தொகை ரூபாய்"))
                    {
                        var newText = pText
                            .Replace("தொகை ரூபாய் ………………………", $"தொகை ரூபாய் ₹ {loan.LoanAmount:N2} /-")
                            .Replace("(Rupees …………………………………………………………)", $"(Rupees {wordsAmount})");
                        ReplaceRunText(tNodes, pText, newText);
                        continue;
                    }

                    // 7. என்னுடைய பிற்கால அவகாசியான திரு/திருமதி...
                    if (pText.Contains("என்னுடைய பிற்கால அவகாசியான திரு/திருமதி"))
                    {
                        var newText = pText.Replace("திரு/திருமதி …………………………", $"திரு/திருமதி {nominee}");
                        ReplaceRunText(tNodes, pText, newText);
                        continue;
                    }

                    // 8. Loan No : (Office copy & Customer copy)
                    if (pText.Contains("Loan No :") && !pText.Contains(loan.LoanNumber))
                    {
                        ReplaceRunText(tNodes, "Loan No :", $"Loan No : {loan.LoanNumber}");
                        continue;
                    }

                    // 9. Amount : ……………… /-
                    if (pText.Contains("Amount :") && pText.Contains("……"))
                    {
                        ReplaceRunText(tNodes, pText, $"Amount : ₹ {loan.LoanAmount:N2} /-");
                        continue;
                    }

                    // 10. Loan Date :
                    if (pText.Contains("Loan Date :") && !pText.Contains(loan.PledgeDate.ToString("dd-MM-yyyy")))
                    {
                        ReplaceRunText(tNodes, "Loan Date :", $"Loan Date : {loan.PledgeDate:dd-MM-yyyy}");
                        continue;
                    }

                    // 11. Customer ID :
                    if (pText.Contains("Customer ID :") && !pText.Contains(customer.Code))
                    {
                        ReplaceRunText(tNodes, "Customer ID :", $"Customer ID : {customer.Code}");
                        continue;
                    }

                    // 12. Address : and Scheme :
                    if (pText.Contains("Address :") && pText.Contains("Scheme :"))
                    {
                        ReplaceRunText(tNodes, pText, $"Address : {customer.Name}, {customer.Address}  |  Scheme : {scheme.Name}");
                        continue;
                    }
                    else if (pText.StartsWith("Address :") && !pText.Contains(customer.Address))
                    {
                        ReplaceRunText(tNodes, "Address :", $"Address : {customer.Address}");
                        continue;
                    }
                    else if (pText.StartsWith("Scheme :") && !pText.Contains(scheme.Name))
                    {
                        ReplaceRunText(tNodes, "Scheme :", $"Scheme : {scheme.Name}");
                        continue;
                    }

                    // 13. Jewel Details :
                    if (pText.Contains("Jewel Details :") && !pText.Contains(totalQty.ToString()))
                    {
                        ReplaceRunText(tNodes, "Jewel Details :", $"Jewel Details : Total Qty: {totalQty}, Net Wt: {totalNet:F3} g");
                        continue;
                    }

                    // 14. Ph :
                    if (pText.Contains("Ph :") && !pText.Contains(customer.Phone))
                    {
                        ReplaceRunText(tNodes, "Ph :", $"Ph : {customer.Phone}");
                        continue;
                    }

                    // 15. Total Qty :
                    if (pText.Contains("Total Qty :") && !pText.Contains(totalQty.ToString()))
                    {
                        ReplaceRunText(tNodes, "Total Qty :", $"Total Qty : {totalQty}");
                        continue;
                    }

                    // 16. Gross Weight :
                    if (pText.Contains("Gross Weight :") && !pText.Contains(totalGross.ToString("F3")))
                    {
                        ReplaceRunText(tNodes, "Gross Weight :", $"Gross Weight : {totalGross:F3} g");
                        continue;
                    }

                    // 17. Net Weight :
                    if (pText.Contains("Net Weight :") && !pText.Contains(totalNet.ToString("F3")))
                    {
                        ReplaceRunText(tNodes, "Net Weight :", $"Net Weight : {totalNet:F3} g");
                        continue;
                    }

                    // 18. திருப்ப வேண்டிய கடைசி தேதி :
                    if (pText.Contains("திருப்ப வேண்டிய கடைசி தேதி :") && !pText.Contains(loan.MaturityDate.ToString("dd-MM-yyyy")))
                    {
                        ReplaceRunText(tNodes, "திருப்ப வேண்டிய கடைசி தேதி :", $"திருப்ப வேண்டிய கடைசி தேதி : {loan.MaturityDate:dd-MM-yyyy}");
                        continue;
                    }

                    // 19. Nominee in Office Declaration: திரு/திருமதி ………………… [உண்மையான
                    if (pText.Contains("திரு/திருமதி") && pText.Contains("[உண்மையான உரிமையாளர்"))
                    {
                        var newText = pText.Replace("திரு/திருமதி …………………", $"திரு/திருமதி {nominee}");
                        ReplaceRunText(tNodes, pText, newText);
                        continue;
                    }
                }

                // Populate Table 5 (Jewel items table in Office Copy)
                PopulateJewelItemsTable(xDoc, items, totalQty, totalGross, totalNet);

                // Save modified XML back to docx zip entry
                entry.Delete();
                var newEntry = zip.CreateEntry("word/document.xml", CompressionLevel.Fastest);
                using (var writer = new StreamWriter(newEntry.Open(), Encoding.UTF8))
                {
                    xDoc.Save(writer);
                }
            }
        }

        return targetPath;
    }

    private static void ReplaceRunText(List<XElement> tNodes, string oldPart, string newFull)
    {
        var text = string.Concat(tNodes.Select(t => t.Value));
        var replaced = text.Contains(oldPart) ? text.Replace(oldPart, newFull) : newFull;

        tNodes[0].Value = replaced;
        for (int i = 1; i < tNodes.Count; i++)
        {
            tNodes[i].Value = string.Empty;
        }
    }

    private static void PopulateJewelItemsTable(XDocument xDoc, List<LoanItem> items, int totalQty, decimal totalGross, decimal totalNet)
    {
        try
        {
            var tables = xDoc.Descendants(W + "tbl").ToList();
            // Find the jewel items table (contains headers: Jewel Name, JType, Qty, G.Weight, N.Weight)
            var jewelTable = tables.FirstOrDefault(t =>
                t.Descendants(W + "t").Any(tNode => tNode.Value.Contains("Jewel Name")) &&
                t.Descendants(W + "t").Any(tNode => tNode.Value.Contains("G.Weight")));

            if (jewelTable == null) return;

            var rows = jewelTable.Descendants(W + "tr").ToList();
            if (rows.Count < 2) return;

            // Row 0 is header, Row 1 is prototype row, Last row is Totals row
            var templateRow = rows.Count >= 3 ? rows[1] : rows.Last();
            var totalsRow = rows.Last();

            // Clear existing data rows between header and totals
            for (int i = 1; i < rows.Count - 1; i++)
            {
                rows[i].Remove();
            }

            if (items.Count == 0)
            {
                // Insert one generic row
                var newRow = new XElement(templateRow);
                SetRowCells(newRow, "Gold Jewel", "Gold", totalQty.ToString(), $"{totalGross:F3}", $"{totalNet:F3}");
                totalsRow.AddBeforeSelf(newRow);
            }
            else
            {
                foreach (var item in items)
                {
                    var newRow = new XElement(templateRow);
                    var jType = item.JewelType?.Name ?? "Gold";
                    var name = string.IsNullOrWhiteSpace(item.Description) ? jType : item.Description;
                    SetRowCells(newRow, name, jType, item.Quantity.ToString(), $"{item.GrossWeight:F3}", $"{item.NetWeight:F3}");
                    totalsRow.AddBeforeSelf(newRow);
                }
            }

            // Ensure totals row values are updated
            var totalsTNodes = totalsRow.Descendants(W + "t").ToList();
            foreach (var t in totalsTNodes)
            {
                if (t.Value.Contains("Total Qty") && !t.Value.Contains(totalQty.ToString()))
                    t.Value = $"Total Qty : {totalQty}";
                else if (t.Value.Contains("Gross Weight") && !t.Value.Contains(totalGross.ToString("F3")))
                    t.Value = $"Gross Weight : {totalGross:F3}";
                else if (t.Value.Contains("Net Weight") && !t.Value.Contains(totalNet.ToString("F3")))
                    t.Value = $"Net Weight : {totalNet:F3}";
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to dynamically populate jewel items table in Word document.");
        }
    }

    private static void SetRowCells(XElement row, string jewelName, string jType, string qty, string gWt, string nWt)
    {
        var cells = row.Descendants(W + "tc").ToList();
        if (cells.Count >= 5)
        {
            SetCellText(cells[0], jewelName);
            SetCellText(cells[1], jType);
            SetCellText(cells[2], qty);
            SetCellText(cells[3], gWt);
            SetCellText(cells[4], nWt);
        }
    }

    private static void SetCellText(XElement cell, string text)
    {
        var tNodes = cell.Descendants(W + "t").ToList();
        if (tNodes.Count > 0)
        {
            tNodes[0].Value = text;
            for (int i = 1; i < tNodes.Count; i++) tNodes[i].Value = string.Empty;
        }
        else
        {
            cell.Add(new XElement(W + "p", new XElement(W + "r", new XElement(W + "t", text))));
        }
    }

    /// <summary>
    /// Generates the filled document and opens it directly in Microsoft Word or default viewer.
    /// </summary>
    public static bool OpenInWord(Loan loan, Customer customer, Scheme scheme)
    {
        try
        {
            var filledPath = GenerateFilledDocument(loan, customer, scheme);
            Process.Start(new ProcessStartInfo(filledPath) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open filled Word document.");
            System.Windows.MessageBox.Show($"Failed to open Word document: {ex.Message}", "Word Document Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// Sends the pre-filled Word document directly to the printer using Microsoft Word automation or shell verb.
    /// </summary>
    public static bool PrintWordDocumentDirectly(Loan loan, Customer customer, Scheme scheme)
    {
        try
        {
            var filledPath = GenerateFilledDocument(loan, customer, scheme);

            // Attempt printing via Word COM
            Type? wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType != null)
            {
                dynamic? wordApp = Activator.CreateInstance(wordType);
                if (wordApp != null)
                {
                    try
                    {
                        wordApp.Visible = false;
                        dynamic doc = wordApp.Documents.Open(filledPath);
                        doc.PrintOut();
                        doc.Close(false);
                        return true;
                    }
                    finally
                    {
                        wordApp.Quit();
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                    }
                }
            }

            // Fallback to shell print verb
            var psi = new ProcessStartInfo(filledPath)
            {
                Verb = "print",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send Word document directly to printer.");
            System.Windows.MessageBox.Show($"Failed to send to printer: {ex.Message}", "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// Converts a filled Word document to high-resolution vector PDF using Word's native engine.
    /// </summary>
    public static bool ExportWordToPdf(Loan loan, Customer customer, Scheme scheme, string targetPdfPath)
    {
        try
        {
            var filledPath = GenerateFilledDocument(loan, customer, scheme);

            Type? wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType != null)
            {
                dynamic? wordApp = Activator.CreateInstance(wordType);
                if (wordApp != null)
                {
                    try
                    {
                        wordApp.Visible = false;
                        dynamic doc = wordApp.Documents.Open(filledPath);
                        doc.SaveAs2(targetPdfPath, 17); // 17 = wdFormatPDF
                        doc.Close(false);
                        return true;
                    }
                    finally
                    {
                        wordApp.Quit();
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Word COM PDF conversion failed; falling back to internal PDF renderer.");
        }
        return false;
    }

    /// <summary>
    /// Shows SaveFileDialog and saves the pre-filled Word document to user's desired location.
    /// </summary>
    public static bool SaveWordDocument(Loan loan, Customer customer, Scheme scheme, System.Windows.Window? owner = null)
    {
        try
        {
            var safeLoanNo = loan.LoanNumber.Replace('/', '_').Replace('\\', '_');
            var sfd = new SaveFileDialog
            {
                Title = "Save Jewel Loan Word Document (ரசீது & கடன் பத்திரம்)",
                Filter = "Word Document (*.docx)|*.docx|All Files (*.*)|*.*",
                FileName = $"JGF_PawnTicket_{safeLoanNo}_{DateTime.Today:yyyyMMdd}.docx"
            };

            if (sfd.ShowDialog(owner) != true) return false;

            GenerateFilledDocument(loan, customer, scheme, sfd.FileName);

            var res = System.Windows.MessageBox.Show(
                $"Jewel Loan Word document saved successfully!\n\nFile: {sfd.FileName}\n\nWould you like to open it now?",
                "Document Saved",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (res == System.Windows.MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save Word document.");
            System.Windows.MessageBox.Show($"Failed to save Word document: {ex.Message}", "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return false;
        }
    }
}

