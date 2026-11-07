using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using JGF.Application.Migration;
using Microsoft.Win32;

namespace JGF.Desktop.Views.Controls;

public partial class DataMigrationControl : UserControl
{
    private readonly IDataMigrationService _migrationService;
    private string? _selectedFilePath;
    private List<MigrationError> _lastErrors = new();

    public DataMigrationControl(IDataMigrationService migrationService)
    {
        InitializeComponent();
        _migrationService = migrationService;
        Loaded += async (s, e) => await RefreshAuditAsync();
    }

    private void OnEntityTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        // Reserved for dynamic field mapping hints
    }

    private void OnBrowseFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "CSV and Excel Files (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|CSV Files (*.csv)|*.csv|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
            Title = "Select Legacy Export Data File"
        };

        if (dlg.ShowDialog() == true)
        {
            _selectedFilePath = dlg.FileName;
            TxtFilePath.Text = _selectedFilePath;
        }
    }

    private async void OnRunDryRunClick(object sender, RoutedEventArgs e)
    {
        await ExecuteMigrationAsync(isDryRun: true);
    }

    private async void OnExecuteMigrationClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Are you sure you want to commit this migrated data to the live database?\n\n" +
            "Ensure you have verified the Dry-Run report first. This will insert real records into the database.",
            "Confirm Live Migration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await ExecuteMigrationAsync(isDryRun: false);
        }
    }

    private async Task ExecuteMigrationAsync(bool isDryRun)
    {
        if (string.IsNullOrWhiteSpace(_selectedFilePath) || !File.Exists(_selectedFilePath))
        {
            MessageBox.Show("Please select a valid CSV or Excel file first.", "File Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            BtnValidate.IsEnabled = false;
            BtnExecute.IsEnabled = false;
            TxtStats.Text = "Processing data migration...";

            var entityType = (MigrationEntityType)CmbEntityType.SelectedIndex;
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var stream = File.OpenRead(_selectedFilePath);
            var fileName = Path.GetFileName(_selectedFilePath);

            MigrationExecutionResult result = entityType switch
            {
                MigrationEntityType.Customers => await _migrationService.MigrateCustomersAsync(stream, fileName, mapping, isDryRun),
                MigrationEntityType.Loans => await _migrationService.MigrateLoansAsync(stream, fileName, mapping, isDryRun),
                MigrationEntityType.Receipts => await _migrationService.MigrateReceiptsAsync(stream, fileName, mapping, isDryRun),
                MigrationEntityType.BankPledges => await _migrationService.MigrateBankPledgesAsync(stream, fileName, mapping, isDryRun),
                MigrationEntityType.LedgerBalances => await _migrationService.MigrateLedgerBalancesAsync(stream, fileName, mapping, isDryRun),
                _ => throw new InvalidOperationException("Invalid entity type")
            };

            _lastErrors = result.Errors;
            DgErrors.ItemsSource = _lastErrors;

            TxtStats.Text = $"Rows Read: {result.TotalRead} | Valid: {result.SuccessfullyProcessed} | Errors: {result.FailedRows} ({(isDryRun ? "Dry-Run" : "Committed")})";

            MessageBox.Show(
                result.SummaryMessage,
                isDryRun ? "Dry-Run Complete" : "Live Migration Finished",
                MessageBoxButton.OK,
                result.FailedRows > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await RefreshAuditAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Migration failed: {ex.Message}", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnValidate.IsEnabled = true;
            BtnExecute.IsEnabled = true;
        }
    }

    private async void OnRefreshAuditClick(object sender, RoutedEventArgs e)
    {
        await RefreshAuditAsync();
    }

    private async Task RefreshAuditAsync()
    {
        try
        {
            decimal.TryParse(TxtOldPrincipal.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oldPrincipal);
            decimal.TryParse(TxtOldInterest.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oldInterest);
            decimal.TryParse(TxtOldCash.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oldCash);
            decimal.TryParse(TxtOldBank.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oldBank);

            var recon = await _migrationService.GenerateReconciliationReportAsync(
                oldPrincipal > 0 ? oldPrincipal : null,
                oldInterest > 0 ? oldInterest : null,
                oldCash > 0 ? oldCash : null,
                oldBank > 0 ? oldBank : null);

            var diffSum = Math.Abs(recon.OutstandingPrincipalDiff) +
                          Math.Abs(recon.InterestReceivedDiff) +
                          Math.Abs(recon.CashBalanceDiff) +
                          Math.Abs(recon.BankRepledgeDiff);

            if (diffSum == 0 && (recon.IntegrityAudit == null || !recon.IntegrityAudit.HasDiscrepancies))
            {
                TxtReconTitle.Text = "✔ Zero Difference Reconciliation Status (Exact Match)";
                BorderReconciliation.Background = System.Windows.Media.Brushes.Honeydew;
                TxtReconTitle.Foreground = System.Windows.Media.Brushes.DarkGreen;
            }
            else
            {
                TxtReconTitle.Text = $"⚠ Difference Detected: Total Discrepancy ₹{diffSum:N2}";
                BorderReconciliation.Background = System.Windows.Media.Brushes.MistyRose;
                TxtReconTitle.Foreground = System.Windows.Media.Brushes.DarkRed;
            }

            TxtReconDetails.Text = $"Principal: ₹{recon.NewOutstandingPrincipal:N2} (Diff: ₹{recon.OutstandingPrincipalDiff:N2}) | " +
                                  $"Interest: ₹{recon.NewInterestReceived:N2} (Diff: ₹{recon.InterestReceivedDiff:N2}) | " +
                                  $"Cash: ₹{recon.NewCashBalance:N2} (Diff: ₹{recon.CashBalanceDiff:N2}) | " +
                                  $"Bank Repledge: ₹{recon.NewBankRepledgeOutstanding:N2} (Diff: ₹{recon.BankRepledgeDiff:N2})";

            if (recon.IntegrityAudit != null)
            {
                TxtIntegrityStatus.Text = $"Accounting Integrity Check: Unbalanced Vouchers: {recon.IntegrityAudit.UnbalancedVouchersCount} | BS Imbalance: ₹{recon.IntegrityAudit.BalanceSheetDifference:N2} | Loan vs Ledger Diff: ₹{recon.IntegrityAudit.LoanModuleVsLedgerDifference:N2}";
            }
        }
        catch { }
    }

    private void OnExportReportClick(object sender, RoutedEventArgs e)
    {
        if (!_lastErrors.Any())
        {
            MessageBox.Show("No error records to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"Migration_Error_Audit_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (dlg.ShowDialog() == true)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Migration Errors");

            ws.Cell(1, 1).Value = "Row Index";
            ws.Cell(1, 2).Value = "Identifier";
            ws.Cell(1, 3).Value = "Column Name";
            ws.Cell(1, 4).Value = "Severity";
            ws.Cell(1, 5).Value = "Message";

            int r = 2;
            foreach (var err in _lastErrors)
            {
                ws.Cell(r, 1).Value = err.RowIndex;
                ws.Cell(r, 2).Value = err.Identifier;
                ws.Cell(r, 3).Value = err.ColumnName;
                ws.Cell(r, 4).Value = err.Severity;
                ws.Cell(r, 5).Value = err.Message;
                r++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(dlg.FileName);
            MessageBox.Show("Migration audit log exported to Excel successfully!", "Exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
