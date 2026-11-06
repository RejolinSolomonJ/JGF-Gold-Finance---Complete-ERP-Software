namespace JGF.Domain.Enums;

/// <summary>
/// Fine-grained permission flags checked across all application services.
/// </summary>
[Flags]
public enum Permission
{
    None = 0,

    // Customer permissions
    ViewCustomer = 1 << 0,
    CreateCustomer = 1 << 1,
    EditCustomer = 1 << 2,
    DeleteCustomer = 1 << 3,

    // Loan permissions
    ViewLoan = 1 << 4,
    CreateLoan = 1 << 5,
    EditLoan = 1 << 6,
    ApproveLoan = 1 << 7,
    CloseLoan = 1 << 8,

    // Receipt permissions
    ViewReceipt = 1 << 9,
    CreateReceipt = 1 << 10,
    CancelReceipt = 1 << 11,

    // Masters permissions
    ManageSchemes = 1 << 12,
    ManageJewelTypes = 1 << 13,
    ManageGoldRates = 1 << 14,
    ManageBanks = 1 << 15,
    ManageNumberSeries = 1 << 16,

    // Accounting & Day-End
    ViewAccounts = 1 << 17,
    ManageVouchers = 1 << 18,
    CloseDay = 1 << 19,
    ReopenDay = 1 << 20,
    BackDatedEntry = 1 << 21,

    // Repledge & Liquidation
    ManageBankRepledge = 1 << 22,
    ManageAuction = 1 << 23,

    // Reports & System
    ViewReports = 1 << 24,
    ViewAuditLogs = 1 << 25,
    ManageUsers = 1 << 26,
    ManageBackups = 1 << 27,
    ManageSettings = 1 << 28
}
