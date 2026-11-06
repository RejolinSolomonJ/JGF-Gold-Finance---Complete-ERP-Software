using ClosedXML.Excel;
using JGF.Application.Reports;

namespace JGF.Infrastructure.Services;

public class ExcelExportService : IExcelExportService
{
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

        // 4. Totals Row (if provided)
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
}
