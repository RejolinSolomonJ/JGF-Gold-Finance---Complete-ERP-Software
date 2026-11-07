using JGF.Application.Accounting;

namespace JGF.Application.Migration;

public enum MigrationEntityType
{
    Customers,
    Loans,
    Receipts,
    BankPledges,
    LedgerBalances
}

public class MigrationError
{
    public int RowIndex { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error"; // Error or Warning
}

public class MigrationReconciliation
{
    public int OldRecordCount { get; set; }
    public int ImportedRecordCount { get; set; }

    public decimal OldOutstandingPrincipal { get; set; }
    public decimal NewOutstandingPrincipal { get; set; }
    public decimal OutstandingPrincipalDiff => NewOutstandingPrincipal - OldOutstandingPrincipal;

    public decimal OldInterestReceived { get; set; }
    public decimal NewInterestReceived { get; set; }
    public decimal InterestReceivedDiff => NewInterestReceived - OldInterestReceived;

    public decimal OldCashBalance { get; set; }
    public decimal NewCashBalance { get; set; }
    public decimal CashBalanceDiff => NewCashBalance - OldCashBalance;

    public decimal OldBankRepledgeOutstanding { get; set; }
    public decimal NewBankRepledgeOutstanding { get; set; }
    public decimal BankRepledgeDiff => NewBankRepledgeOutstanding - OldBankRepledgeOutstanding;

    public AccountingIntegrityCheckDto? IntegrityAudit { get; set; }
    public bool IsBalanced => OutstandingPrincipalDiff == 0 &&
                              InterestReceivedDiff == 0 &&
                              CashBalanceDiff == 0 &&
                              BankRepledgeDiff == 0 &&
                              (IntegrityAudit == null || !IntegrityAudit.HasDiscrepancies);
}

public class MigrationExecutionResult
{
    public MigrationEntityType EntityType { get; set; }
    public bool IsDryRun { get; set; }
    public int TotalRead { get; set; }
    public int SuccessfullyProcessed { get; set; }
    public int FailedRows { get; set; }
    public List<MigrationError> Errors { get; set; } = new();
    public MigrationReconciliation? Reconciliation { get; set; }
    public string SummaryMessage { get; set; } = string.Empty;
}

public interface IDataMigrationService
{
    Task<List<string>> InspectFileColumnsAsync(Stream fileStream, string fileName);

    Task<MigrationExecutionResult> MigrateCustomersAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun);

    Task<MigrationExecutionResult> MigrateLoansAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun);

    Task<MigrationExecutionResult> MigrateReceiptsAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun);

    Task<MigrationExecutionResult> MigrateBankPledgesAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun);

    Task<MigrationExecutionResult> MigrateLedgerBalancesAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun);

    Task<MigrationReconciliation> GenerateReconciliationReportAsync(
        decimal? expectedPrincipal = null,
        decimal? expectedInterest = null,
        decimal? expectedCash = null,
        decimal? expectedBankOutstanding = null);
}
