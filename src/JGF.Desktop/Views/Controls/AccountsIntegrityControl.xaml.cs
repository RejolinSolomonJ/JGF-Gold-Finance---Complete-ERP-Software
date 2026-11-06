using System.Windows;
using System.Windows.Controls;
using JGF.Application.Accounting;

namespace JGF.Desktop.Views.Controls;

public partial class AccountsIntegrityControl : UserControl
{
    private readonly IAccountingReportService _accountingReport;

    public AccountsIntegrityControl(IAccountingReportService accountingReport)
    {
        InitializeComponent();
        _accountingReport = accountingReport;
        DpAuditDate.SelectedDate = DateTime.Today;

        Loaded += async (s, e) => await RunAuditAsync();
    }

    private async void OnRunAuditClick(object sender, RoutedEventArgs e)
    {
        await RunAuditAsync();
    }

    private async Task RunAuditAsync()
    {
        var date = DateOnly.FromDateTime(DpAuditDate.SelectedDate ?? DateTime.Today);
        var audit = await _accountingReport.RunIntegrityAuditAsync(date);

        // 1. Unbalanced vouchers
        TxtUnbalancedCount.Text = $"{audit.UnbalancedVouchersCount} Unbalanced Vouchers";
        TxtVoucherStatus.Text = audit.UnbalancedVouchersCount == 0 ? "[PASS]" : "[FAIL]";
        TxtVoucherStatus.Foreground = audit.UnbalancedVouchersCount == 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Crimson;

        // 2. Balance Sheet
        TxtBsDiff.Text = $"Difference: ₹ {audit.BalanceSheetDifference:N2}";
        TxtBsStatus.Text = audit.BalanceSheetDifference < 0.01m ? "[PASS]" : "[FAIL]";
        TxtBsStatus.Foreground = audit.BalanceSheetDifference < 0.01m ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Crimson;

        // 3. Cash Reconciliation
        TxtCashReconcile.Text = $"DayBook=₹{audit.CashBookClosingCash:N2} | BS=₹{audit.BalanceSheetCashAsset:N2}";
        TxtCashStatus.Text = audit.CashReconciliationDifference < 0.01m ? "[PASS]" : "[FAIL]";
        TxtCashStatus.Foreground = audit.CashReconciliationDifference < 0.01m ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Crimson;

        // 4. Loan Module vs Ledger
        TxtLoanReconcile.Text = $"Loans=₹{audit.LoanModulePrincipalOutstanding:N2} | Ledger=₹{audit.JewelLoanLedgerHeadBalance:N2}";
        TxtLoanStatus.Text = audit.LoanModuleVsLedgerDifference < 0.01m ? "[PASS]" : "[FAIL]";
        TxtLoanStatus.Foreground = audit.LoanModuleVsLedgerDifference < 0.01m ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Crimson;

        // 5. Repledge vs Ledger
        TxtRepledgeReconcile.Text = $"Repledge=₹{audit.BankRepledgeModuleOutstanding:N2} | Ledger=₹{audit.BankRepledgeLedgerHeadBalance:N2}";
        TxtRepledgeStatus.Text = audit.BankRepledgeVsLedgerDifference < 0.01m ? "[PASS]" : "[FAIL]";
        TxtRepledgeStatus.Foreground = audit.BankRepledgeVsLedgerDifference < 0.01m ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Crimson;

        // Overall banner
        if (!audit.HasDiscrepancies)
        {
            BannerStatus.Background = System.Windows.Media.Brushes.Honeydew;
            BannerStatus.BorderBrush = System.Windows.Media.Brushes.DarkGreen;
            TxtBannerIcon.Text = "✓";
            TxtBannerIcon.Foreground = System.Windows.Media.Brushes.DarkGreen;
            TxtBannerTitle.Text = "ALL SYSTEM INTEGRITY CHECKS PASSED (ZERO DISCREPANCIES)";
            TxtBannerTitle.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }
        else
        {
            BannerStatus.Background = System.Windows.Media.Brushes.MistyRose;
            BannerStatus.BorderBrush = System.Windows.Media.Brushes.Crimson;
            TxtBannerIcon.Text = "⚠";
            TxtBannerIcon.Foreground = System.Windows.Media.Brushes.Crimson;
            TxtBannerTitle.Text = "INTEGRITY WARNING: ACCOUNTING DISCREPANCIES DETECTED";
            TxtBannerTitle.Foreground = System.Windows.Media.Brushes.Crimson;
        }
    }
}

