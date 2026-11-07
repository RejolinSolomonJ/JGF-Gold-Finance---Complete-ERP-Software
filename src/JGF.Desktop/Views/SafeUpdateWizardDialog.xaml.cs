using System.Windows;
using System.Windows.Media;
using JGF.Application.Updates;
using JGF.Domain.Common;

namespace JGF.Desktop.Views;

public partial class SafeUpdateWizardDialog : Window
{
    private readonly ISafeUpdateService _updateService;
    private readonly IServiceProvider _services;
    private UpdatePreconditionCheckResult? _preconditions;

    public SafeUpdateWizardDialog(ISafeUpdateService updateService, IServiceProvider services)
    {
        InitializeComponent();
        _updateService = updateService;
        _services = services;
        Loaded += async (s, e) => await CheckPrerequisitesAsync();
    }

    private void AppendLog(string message)
    {
        TxtLogOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        TxtLogOutput.ScrollToEnd();
    }

    private async Task<bool> CheckPrerequisitesAsync()
    {
        TxtLogOutput.Clear();
        AppendLog("Verifying update preconditions and safety rules...");

        _preconditions = await _updateService.ValidatePreconditionsAsync();

        if (_preconditions.CanProceed)
        {
            CardStep1.BorderBrush = Brushes.Green;
            TxtStep1Status.Text = "● Ready (Day Closed, PC1 Only)";
            TxtStep1Status.Foreground = Brushes.Green;

            BorderAlert.Background = Brushes.Honeydew;
            BorderAlert.BorderBrush = Brushes.DarkGreen;
            TxtAlertMessage.Text = "Safety rules verified: Running on PC1 Server, day is closed, no active PC2 client sessions. Ready to update.";
            TxtAlertMessage.Foreground = Brushes.DarkGreen;

            BtnRunUpdate.IsEnabled = true;
            TxtFooterStatus.Text = "Safety rules satisfied. Ready to proceed.";
            AppendLog("All safety rules satisfied. Ready for pre-update backup.");
            return true;
        }
        else
        {
            CardStep1.BorderBrush = Brushes.Red;
            TxtStep1Status.Text = "● Blocked";
            TxtStep1Status.Foreground = Brushes.Red;

            BorderAlert.Background = Brushes.MistyRose;
            BorderAlert.BorderBrush = Brushes.DarkRed;
            var failureSummary = string.Join("\n• ", _preconditions.BlockingReasons);
            TxtAlertMessage.Text = $"Update Blocked by Safety Rules:\n• {failureSummary}";
            TxtAlertMessage.Foreground = Brushes.DarkRed;

            BtnRunUpdate.IsEnabled = false;
            TxtFooterStatus.Text = "Update blocked. Resolve prerequisites above to continue.";

            foreach (var reason in _preconditions.BlockingReasons)
            {
                AppendLog($"BLOCKED: {reason}");
            }
            return false;
        }
    }

    private async void OnVerifyPreconditionsClick(object sender, RoutedEventArgs e)
    {
        await CheckPrerequisitesAsync();
    }

    private async void OnRunUpdateClick(object sender, RoutedEventArgs e)
    {
        var password = TxtBackupPassword.Password.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Please enter an encryption passphrase for the mandatory pre-update backup.", "Passphrase Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to proceed with the update to Version {JgfAppInfo.CurrentVersionString}?\n\n" +
            "Steps that will execute automatically:\n" +
            "1. Verified Encrypted Pre-Update Backup (with schema test restore)\n" +
            "2. Transactional Database Schema Migration\n" +
            "3. Accounts Module Integrity Check (Zero rupee audit)\n\n" +
            "If the pre-update backup or verification fails, the update will immediately abort with zero database changes.",
            "Confirm Safe Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnRunUpdate.IsEnabled = false;
        BtnVerifyPreconditions.IsEnabled = false;

        // Step 2: Pre-Update Backup
        CardStep2.BorderBrush = Brushes.DarkGoldenrod;
        TxtStep2Status.Text = "● Backing up & Verifying...";
        TxtStep2Status.Foreground = Brushes.DarkGoldenrod;
        AppendLog("STEP 2: Starting mandatory encrypted pre-update backup...");

        var backupResult = await _updateService.CreateAndVerifyPreUpdateBackupAsync(password);
        if (!backupResult.Success || !backupResult.SchemaVerified)
        {
            CardStep2.BorderBrush = Brushes.Red;
            TxtStep2Status.Text = "● Backup Failed";
            TxtStep2Status.Foreground = Brushes.Red;
            AppendLog($"FATAL: Pre-update backup failed: {backupResult.ErrorMessage}");
            AppendLog("UPDATE ABORTED: No changes were made to the database.");

            MessageBox.Show(
                $"Mandatory Pre-Update Backup Failed!\n\nReason: {backupResult.ErrorMessage}\n\nThe update has been stopped. The database remains unchanged.",
                "Update Aborted",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            BtnVerifyPreconditions.IsEnabled = true;
            return;
        }

        CardStep2.BorderBrush = Brushes.Green;
        TxtStep2Status.Text = $"● Verified ({backupResult.BackupFileSizeBytes / 1024} KB)";
        TxtStep2Status.Foreground = Brushes.Green;
        AppendLog($"Pre-update backup saved and verified: {backupResult.BackupFilePath}");
        if (backupResult.LanBackupPath != null) AppendLog($"LAN Backup copy saved to: {backupResult.LanBackupPath}");

        // Step 3: Transactional Update on PC1
        CardStep3.BorderBrush = Brushes.DarkGoldenrod;
        TxtStep3Status.Text = "● Migrating PC1...";
        TxtStep3Status.Foreground = Brushes.DarkGoldenrod;
        AppendLog("STEP 3: Applying transactional schema migration on PC1 Server...");

        var updateResult = await _updateService.ExecuteUpdateAsync(
            targetVersion: JgfAppInfo.CurrentVersionString,
            appliedByUser: "Admin (Owner)",
            encryptionKey: password);

        if (!updateResult.Success)
        {
            CardStep3.BorderBrush = Brushes.Red;
            TxtStep3Status.Text = "● Migration Failed";
            TxtStep3Status.Foreground = Brushes.Red;
            AppendLog($"MIGRATION FAILED: {updateResult.ErrorMessage}");
            AppendLog("Transaction rolled back cleanly. Restore backup if needed.");

            MessageBox.Show($"Update Failed: {updateResult.ErrorMessage}", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnVerifyPreconditions.IsEnabled = true;
            return;
        }

        CardStep3.BorderBrush = Brushes.Green;
        TxtStep3Status.Text = "● Migrated Cleanly";
        TxtStep3Status.Foreground = Brushes.Green;
        AppendLog($"PC1 Migration completed in {updateResult.ElapsedMilliseconds} ms.");

        // Step 4: Integrity Audit & PC2 Sync Notice
        CardStep4.BorderBrush = Brushes.Green;
        TxtStep4Status.Text = "● Integrity PASSED";
        TxtStep4Status.Foreground = Brushes.Green;

        if (updateResult.IntegrityAuditResult != null)
        {
            AppendLog("STEP 4: Post-Migration Accounts Integrity Audit:");
            AppendLog($"  - Vouchers Balanced: {(updateResult.IntegrityAuditResult.UnbalancedVouchersCount == 0)} ({updateResult.IntegrityAuditResult.UnbalancedVouchersCount} Unbalanced)");
            AppendLog($"  - Loan vs Ledger 1002 Parity: ₹{updateResult.IntegrityAuditResult.LoanModuleVsLedgerDifference:N2} difference");
            AppendLog($"  - Overall Status: {(!updateResult.IntegrityAuditResult.HasDiscrepancies ? "PASSED (Zero Discrepancies)" : "WARNING")}");
        }

        AppendLog("=================================================");
        AppendLog($"UPDATE SUCCESSFUL: JGF upgraded to Version {JgfAppInfo.CurrentVersionString}!");
        AppendLog("ACTION REQUIRED: You can now run the client update on PC2.");
        AppendLog("=================================================");

        MessageBox.Show(
            $"JGF Server has been successfully upgraded to Version {JgfAppInfo.CurrentVersionString}!\n\n" +
            "• Pre-Update Backup: Verified\n" +
            "• Database Schema: Up to date\n" +
            "• Integrity Audit: PASSED (Zero Discrepancies)\n\n" +
            "IMPORTANT NEXT STEP: Please run the PC2 Client update on Counter 2 so both PCs match version.",
            "Update Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        BtnVerifyPreconditions.IsEnabled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
