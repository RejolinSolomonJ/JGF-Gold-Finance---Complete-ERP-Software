using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using JGF.Application.Reports;
using JGF.Domain.Entities;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class ExcelExportService : IExcelExportService
{
    private readonly IDbContextFactory<JgfDbContext>? _contextFactory;

    public ExcelExportService()
    {
    }

    public ExcelExportService(IDbContextFactory<JgfDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public byte[] ExportToExcel(
        string title,
        string subtitle,
        List<ExcelColumnDefinition> columns,
        List<List<object?>> rows,
        List<object?>? totalsRow = null)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Report");
        ws.ShowGridLines = true;

        // 1. Title & Subtitle Headers
        ws.Cell(1, 1).Value = "JGF JEWEL LOAN FINANCE — தமிழ்நாடு";
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1E3A8A");

        ws.Cell(2, 1).Value = $"{title.ToUpperInvariant()} | {subtitle}";
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#4B5563");

        ws.Cell(3, 1).Value = $"Generated on: {DateTime.Now:dd-MMM-yyyy hh:mm tt}";
        ws.Cell(3, 1).Style.Font.FontSize = 9;
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#6B7280");

        // 2. Table Column Headers
        int startRow = 5;
        for (int c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(startRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
            cell.Style.Alignment.Horizontal = columns[c].IsNumeric ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94A3B8");
        }
        ws.Row(startRow).Height = 24;

        // 3. Data Rows
        int currentRow = startRow + 1;
        for (int r = 0; r < rows.Count; r++)
        {
            var rowData = rows[r];
            var xlRow = ws.Row(currentRow);
            xlRow.Height = 19;

            var isEven = (r % 2 == 1);
            var rowBg = isEven ? XLColor.FromHtml("#F8FAFC") : XLColor.White;

            for (int c = 0; c < columns.Count; c++)
            {
                var val = (c < rowData.Count) ? rowData[c] : null;
                var cell = ws.Cell(currentRow, c + 1);

                cell.Style.Fill.BackgroundColor = rowBg;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
                cell.Style.Font.FontSize = 9.5;

                if (val == null)
                {
                    cell.Value = "-";
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else if (val is decimal decVal)
                {
                    cell.Value = decVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0.00";
                }
                else if (val is int intVal)
                {
                    cell.Value = intVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0";
                }
                else if (val is DateOnly dateVal)
                {
                    cell.Value = dateVal.ToDateTime(TimeOnly.MinValue);
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "dd-MMM-yyyy";
                }
                else if (val is DateTime dtVal)
                {
                    cell.Value = dtVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "dd-MMM-yyyy";
                }
                else
                {
                    cell.Value = val.ToString();
                    cell.Style.Alignment.Horizontal = columns[c].IsNumeric ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
                }
            }

            currentRow++;
        }

        // 4. Totals Row
        if (totalsRow != null && totalsRow.Count > 0)
        {
            var tRow = ws.Row(currentRow);
            tRow.Height = 22;

            for (int c = 0; c < columns.Count; c++)
            {
                var val = (c < totalsRow.Count) ? totalsRow[c] : null;
                var cell = ws.Cell(currentRow, c + 1);

                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 10;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                cell.Style.Border.TopBorderColor = XLColor.FromHtml("#475569");
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#475569");

                if (val == null)
                {
                    cell.Value = string.Empty;
                }
                else if (val is decimal decVal)
                {
                    cell.Value = decVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0.00";
                }
                else if (val is int intVal)
                {
                    cell.Value = intVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0";
                }
                else
                {
                    cell.Value = val.ToString();
                    cell.Style.Alignment.Horizontal = columns[c].IsNumeric ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
                }
            }
        }

        // 5. Column Widths
        for (int c = 0; c < columns.Count; c++)
        {
            ws.Column(c + 1).AdjustToContents(startRow, currentRow);
            if (ws.Column(c + 1).Width < columns[c].Width)
            {
                ws.Column(c + 1).Width = columns[c].Width;
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportFullDatabaseToExcelAsync(CancellationToken ct = default)
    {
        if (_contextFactory == null)
            throw new InvalidOperationException("DbContextFactory is required for full database export.");

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        using var workbook = new XLWorkbook();

        // 1. CUSTOMERS SHEET
        var customers = await ctx.Customers.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        var custCols = new List<ExcelColumnDefinition>
        {
            new("Code", 12),
            new("Full Name (English)", 24),
            new("பெயர் (Tamil)", 22),
            new("Mobile Number", 15),
            new("Aadhaar Number", 16),
            new("Father / Husband Name", 22),
            new("Address", 30),
            new("City / Town", 16),
            new("District", 16),
            new("Pincode", 10),
            new("Registered Date", 14, "dd-MMM-yyyy")
        };
        var custRows = customers.Select(c => new List<object?>
        {
            c.Code,
            c.Name,
            c.NameTamil ?? "-",
            c.Phone,
            c.AadhaarNumber ?? "-",
            c.FatherOrHusbandName ?? "-",
            c.Address ?? "-",
            c.City ?? "-",
            c.District ?? "-",
            c.Pincode ?? "-",
            c.CreatedAtUtc.ToString("dd-MMM-yyyy")
        }).ToList();
        AddSheet(workbook, "Customers", "Registered Borrowers & KYC Master", custCols, custRows);

        // 2. LOANS SHEET
        var loans = await ctx.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.Scheme)
            .OrderByDescending(l => l.PledgeDate)
            .ToListAsync(ct);
        var loanCols = new List<ExcelColumnDefinition>
        {
            new("Loan Number", 16),
            new("Pledge Date", 14, "dd-MMM-yyyy"),
            new("Customer Code", 14),
            new("Customer Name", 24),
            new("Mobile Number", 15),
            new("Scheme", 20),
            new("Disbursed (₹)", 16, "₹ #,##0.00", true),
            new("Net Weight (g)", 14, "0.000", true),
            new("Market Value (₹)", 16, "₹ #,##0.00", true),
            new("Status", 14),
            new("Locker / Barcode", 16),
            new("Nominee", 18),
            new("Closed Date", 14, "dd-MMM-yyyy")
        };
        var loanRows = loans.Select(l => new List<object?>
        {
            l.LoanNumber,
            l.PledgeDate,
            l.Customer?.Code ?? "-",
            l.Customer?.Name ?? "-",
            l.Customer?.Phone ?? "-",
            l.Scheme?.Name ?? "-",
            l.LoanAmount,
            l.NetWeight,
            l.MarketValue,
            l.Status.ToString(),
            l.PacketNumber ?? "-",
            l.NomineeName ?? "-",
            l.ClosedDate
        }).ToList();
        var loanTotals = new List<object?>
        {
            "TOTALS", null, null, null, null, null,
            loans.Sum(l => l.LoanAmount),
            loans.Sum(l => l.NetWeight),
            loans.Sum(l => l.MarketValue),
            null, null, null, null
        };
        AddSheet(workbook, "Loans", "Jewel Loan Portfolio Registry", loanCols, loanRows, loanTotals);

        // 3. JEWEL ITEMS SHEET
        var items = await ctx.LoanItems.AsNoTracking()
            .Include(i => i.Loan)
            .Include(i => i.JewelType)
            .OrderBy(i => i.Loan.LoanNumber)
            .ToListAsync(ct);
        var itemCols = new List<ExcelColumnDefinition>
        {
            new("Loan Number", 16),
            new("Jewel Type", 18),
            new("Description", 26),
            new("Quantity", 10, "#,##0", true),
            new("Gross Wt (g)", 14, "0.000", true),
            new("Deduct Wt (g)", 14, "0.000", true),
            new("Net Wt (g)", 14, "0.000", true),
            new("Purity %", 12, "0.00%", true),
            new("Rate/g (₹)", 14, "₹ #,##0.00", true),
            new("Appraised Value (₹)", 18, "₹ #,##0.00", true)
        };
        var itemRows = items.Select(i => new List<object?>
        {
            i.Loan?.LoanNumber ?? "-",
            i.JewelType?.Name ?? "-",
            i.Description,
            i.Quantity,
            i.GrossWeight,
            i.DeductionWeight,
            i.NetWeight,
            i.PurityPercent / 100m,
            i.RatePerGram,
            i.AppraisedValue
        }).ToList();
        var itemTotals = new List<object?>
        {
            "TOTALS", null, null,
            items.Sum(i => i.Quantity),
            items.Sum(i => i.GrossWeight),
            items.Sum(i => i.DeductionWeight),
            items.Sum(i => i.NetWeight),
            null, null,
            items.Sum(i => i.AppraisedValue)
        };
        AddSheet(workbook, "Jewel Collateral", "Pledged Gold Ornaments Breakdown", itemCols, itemRows, itemTotals);

        // 4. RECEIPTS SHEET
        var receipts = await ctx.Receipts.AsNoTracking()
            .Include(r => r.Loan).ThenInclude(l => l.Customer)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync(ct);
        var rCols = new List<ExcelColumnDefinition>
        {
            new("Receipt No", 18),
            new("Receipt Date", 14, "dd-MMM-yyyy"),
            new("Loan Number", 16),
            new("Customer Name", 24),
            new("Receipt Type", 16),
            new("Pay Mode", 14),
            new("Principal Paid (₹)", 16, "₹ #,##0.00", true),
            new("Interest Paid (₹)", 16, "₹ #,##0.00", true),
            new("Penal Paid (₹)", 14, "₹ #,##0.00", true),
            new("Postal / Fees (₹)", 14, "₹ #,##0.00", true),
            new("Total Collected (₹)", 18, "₹ #,##0.00", true)
        };
        var rRows = receipts.Select(r => new List<object?>
        {
            r.ReceiptNumber,
            r.ReceiptDate,
            r.Loan?.LoanNumber ?? "-",
            r.Loan?.Customer?.Name ?? "-",
            r.ReceiptType.ToString(),
            r.PaymentMode.ToString(),
            r.PrincipalAmount,
            r.InterestAmount,
            r.PenalInterestAmount,
            r.PostalCharges,
            r.TotalAmount
        }).ToList();
        var rTotals = new List<object?>
        {
            "TOTALS", null, null, null, null, null,
            receipts.Sum(r => r.PrincipalAmount),
            receipts.Sum(r => r.InterestAmount),
            receipts.Sum(r => r.PenalInterestAmount),
            receipts.Sum(r => r.PostalCharges),
            receipts.Sum(r => r.TotalAmount)
        };
        AddSheet(workbook, "Receipts", "Payment Collections & Interest Vouchers", rCols, rRows, rTotals);

        // 5. LOAN SCHEMES SHEET
        var schemes = await ctx.Schemes.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        var sCols = new List<ExcelColumnDefinition>
        {
            new("Scheme Name", 24),
            new("Interest Rate (%/mo)", 18, "0.00%", true),
            new("Max LTV %", 14, "0.00%", true),
            new("Min Days", 12, "#,##0", true),
            new("Tenure (Months)", 14, "#,##0", true),
            new("Is Active", 12)
        };
        var sRows = schemes.Select(s => new List<object?>
        {
            s.Name,
            s.InterestRatePerMonth / 100m,
            s.MaxLtvPercent / 100m,
            s.MinimumInterestDays,
            s.MaxTenureMonths,
            s.IsActive ? "Yes" : "No"
        }).ToList();
        AddSheet(workbook, "Loan Schemes", "Product Configuration & Interest Rules", sCols, sRows);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AddSheet(
        XLWorkbook wb,
        string sheetName,
        string subtitle,
        List<ExcelColumnDefinition> columns,
        List<List<object?>> rows,
        List<object?>? totalsRow = null)
    {
        var ws = wb.Worksheets.Add(sheetName);
        ws.ShowGridLines = true;

        // Title Block
        ws.Cell(1, 1).Value = "JEBA GOLD FINANCE (JGF) — தமிழ்நாடு";
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1E3A8A");

        ws.Cell(2, 1).Value = $"{sheetName.ToUpperInvariant()} | {subtitle}";
        ws.Cell(2, 1).Style.Font.FontSize = 10;
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#475569");

        ws.Cell(3, 1).Value = $"Database Snapshot: {DateTime.Now:dd-MMM-yyyy hh:mm tt}";
        ws.Cell(3, 1).Style.Font.FontSize = 8.5;
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#64748B");

        int startRow = 5;
        for (int c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(startRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
            cell.Style.Alignment.Horizontal = columns[c].IsNumeric ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94A3B8");
        }
        ws.Row(startRow).Height = 22;

        int currentRow = startRow + 1;
        for (int r = 0; r < rows.Count; r++)
        {
            var rowData = rows[r];
            var xlRow = ws.Row(currentRow);
            xlRow.Height = 18;

            var isEven = (r % 2 == 1);
            var rowBg = isEven ? XLColor.FromHtml("#F8FAFC") : XLColor.White;

            for (int c = 0; c < columns.Count; c++)
            {
                var val = (c < rowData.Count) ? rowData[c] : null;
                var cell = ws.Cell(currentRow, c + 1);

                cell.Style.Fill.BackgroundColor = rowBg;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
                cell.Style.Font.FontSize = 9.5;

                if (val == null)
                {
                    cell.Value = "-";
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else if (val is decimal decVal)
                {
                    cell.Value = decVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0.00";
                }
                else if (val is int intVal)
                {
                    cell.Value = intVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0";
                }
                else if (val is DateOnly dateVal)
                {
                    cell.Value = dateVal.ToDateTime(TimeOnly.MinValue);
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "dd-MMM-yyyy";
                }
                else if (val is DateTime dtVal)
                {
                    cell.Value = dtVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "dd-MMM-yyyy";
                }
                else
                {
                    cell.Value = val.ToString();
                    cell.Style.Alignment.Horizontal = columns[c].IsNumeric ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
                }
            }

            currentRow++;
        }

        if (totalsRow != null && totalsRow.Count > 0)
        {
            var tRow = ws.Row(currentRow);
            tRow.Height = 22;

            for (int c = 0; c < columns.Count; c++)
            {
                var val = (c < totalsRow.Count) ? totalsRow[c] : null;
                var cell = ws.Cell(currentRow, c + 1);

                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 10;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                cell.Style.Border.TopBorderColor = XLColor.FromHtml("#475569");
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#475569");

                if (val == null)
                {
                    cell.Value = string.Empty;
                }
                else if (val is decimal decVal)
                {
                    cell.Value = decVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0.00";
                }
                else if (val is int intVal)
                {
                    cell.Value = intVal;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cell.Style.NumberFormat.Format = columns[c].Format ?? "#,##0";
                }
                else
                {
                    cell.Value = val.ToString();
                    cell.Style.Alignment.Horizontal = columns[c].IsNumeric ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
                }
            }
        }

        for (int c = 0; c < columns.Count; c++)
        {
            ws.Column(c + 1).AdjustToContents(startRow, currentRow);
            if (ws.Column(c + 1).Width < columns[c].Width)
            {
                ws.Column(c + 1).Width = columns[c].Width;
            }
        }
    }
}
