namespace JGF.Application.Reports;

public record ExcelColumnDefinition(
    string Header,
    int Width = 15,
    string? Format = null, // e.g. "₹ #,##0.00", "0.000", "dd-MMM-yyyy"
    bool IsNumeric = false
);

public interface IExcelExportService
{
    byte[] ExportToExcel(
        string title,
        string subtitle,
        List<ExcelColumnDefinition> columns,
        List<List<object?>> rows,
        List<object?>? totalsRow = null
    );
}
