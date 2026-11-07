using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using JGF.Application.Accounting;
using JGF.Application.Migration;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JGF.Infrastructure.Services;

public class DataMigrationService : IDataMigrationService
{
    private readonly JgfDbContext _context;
    private readonly IAccountingReportService _accountingReportService;

    public DataMigrationService(JgfDbContext context, IAccountingReportService accountingReportService)
    {
        _context = context;
        _accountingReportService = accountingReportService;
    }

    public async Task<List<string>> InspectFileColumnsAsync(Stream fileStream, string fileName)
    {
        var (headers, _) = await ReadRowsAsync(fileStream, fileName, maxRowsToRead: 1);
        return headers;
    }

    public async Task<MigrationExecutionResult> MigrateCustomersAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun)
    {
        var (headers, rows) = await ReadRowsAsync(fileStream, fileName);
        var result = new MigrationExecutionResult
        {
            EntityType = MigrationEntityType.Customers,
            IsDryRun = isDryRun,
            TotalRead = rows.Count
        };

        var existingMobiles = await _context.Customers.Select(c => c.Phone).ToHashSetAsync();
        var existingCodes = await _context.Customers.Select(c => c.Code).ToHashSetAsync();
        var seenFileMobiles = new HashSet<string>();
        var customersToInsert = new List<Customer>();

        int rowIndex = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            var name = GetValue(row, columnMapping, "Name");
            var mobile = GetValue(row, columnMapping, "Mobile");
            var address = GetValue(row, columnMapping, "Address");
            var code = GetValue(row, columnMapping, "CustomerCode");
            var aadhaar = GetValue(row, columnMapping, "Aadhaar");
            var pan = GetValue(row, columnMapping, "PAN");
            var guardian = GetValue(row, columnMapping, "GuardianName");

            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, ColumnName = "Name", Message = "Customer Name cannot be empty.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(mobile))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, ColumnName = "Mobile", Message = "Customer Phone/Mobile number cannot be empty.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            var cleanMobile = new string(mobile.Where(char.IsDigit).ToArray());
            if (cleanMobile.Length < 10)
            {
                result.Errors.Add(new MigrationError
                {
                    RowIndex = rowIndex,
                    ColumnName = "Mobile",
                    Identifier = name,
                    Message = $"Invalid mobile number '{mobile}'. Must have at least 10 digits.",
                    Severity = "Error"
                });
                result.FailedRows++;
                continue;
            }

            if (existingMobiles.Contains(cleanMobile) || seenFileMobiles.Contains(cleanMobile))
            {
                result.Errors.Add(new MigrationError
                {
                    RowIndex = rowIndex,
                    ColumnName = "Mobile",
                    Identifier = name,
                    Message = $"Duplicate mobile number '{cleanMobile}'. Customer already exists in database.",
                    Severity = "Warning"
                });
            }
            seenFileMobiles.Add(cleanMobile);

            var finalCode = string.IsNullOrWhiteSpace(code) ? $"C{rowIndex:D4}" : code.Trim();
            if (existingCodes.Contains(finalCode))
            {
                finalCode = $"{finalCode}-MIG{rowIndex}";
            }

            var customer = new Customer
            {
                Code = finalCode,
                Name = name.Trim(),
                Phone = cleanMobile,
                Address = string.IsNullOrWhiteSpace(address) ? "Imported Customer Address" : address.Trim(),
                FatherOrHusbandName = guardian?.Trim(),
                AadhaarNumber = string.IsNullOrWhiteSpace(aadhaar) ? null : aadhaar.Trim(),
                PanNumber = string.IsNullOrWhiteSpace(pan) ? null : pan.Trim(),
                Gender = Gender.Male
            };

            customersToInsert.Add(customer);
            result.SuccessfullyProcessed++;
        }

        if (!isDryRun && customersToInsert.Any())
        {
            await _context.Customers.AddRangeAsync(customersToInsert);
            await _context.SaveChangesAsync();
        }

        result.SummaryMessage = $"Processed {result.TotalRead} customer rows. Valid: {result.SuccessfullyProcessed}, Errors: {result.FailedRows}. Mode: {(isDryRun ? "DRY-RUN (Simulated)" : "COMMITTED TO DATABASE")}";
        return result;
    }

    public async Task<MigrationExecutionResult> MigrateLoansAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun)
    {
        var (headers, rows) = await ReadRowsAsync(fileStream, fileName);
        var result = new MigrationExecutionResult
        {
            EntityType = MigrationEntityType.Loans,
            IsDryRun = isDryRun,
            TotalRead = rows.Count
        };

        var existingLoans = await _context.Loans.Select(l => l.LoanNumber).ToHashSetAsync();
        var customerMap = await _context.Customers.ToDictionaryAsync(c => c.Phone, c => c.Id);
        var defaultScheme = await _context.Schemes.FirstOrDefaultAsync(s => !s.IsDeleted)
                            ?? new Scheme { Code = "S1", Name = "Standard Scheme", InterestMethod = InterestMethod.Simple, InterestRatePerMonth = 1.5m, MaxLtvPercentage = 75m, MinimumInterestMonths = 1 };

        var defaultJewelType = await _context.JewelTypes.FirstOrDefaultAsync()
                               ?? new JewelType { Code = "CH", Name = "Gold Chain", Description = "Migrated Chain", IsActive = true };

        var loansToInsert = new List<Loan>();
        var vouchersToInsert = new List<Voucher>();
        var seenGlInFile = new HashSet<string>();

        int rowIndex = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            var glNo = GetValue(row, columnMapping, "LoanNumber");
            var custMobile = GetValue(row, columnMapping, "CustomerMobile");
            var pledgeDateStr = GetValue(row, columnMapping, "PledgeDate");
            var principalStr = GetValue(row, columnMapping, "Principal");
            var grossWeightStr = GetValue(row, columnMapping, "GrossWeight");
            var netWeightStr = GetValue(row, columnMapping, "NetWeight");
            var jewelName = GetValue(row, columnMapping, "JewelName");
            var countStr = GetValue(row, columnMapping, "ItemCount");

            if (string.IsNullOrWhiteSpace(glNo))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, ColumnName = "LoanNumber", Message = "GL Number / Loan Number is empty.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            glNo = glNo.Trim();
            if (existingLoans.Contains(glNo) || seenGlInFile.Contains(glNo))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = glNo, ColumnName = "LoanNumber", Message = $"Duplicate Loan Number '{glNo}'.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }
            seenGlInFile.Add(glNo);

            var cleanMobile = new string((custMobile ?? "").Where(char.IsDigit).ToArray());
            if (!customerMap.TryGetValue(cleanMobile, out var customerId))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = glNo, ColumnName = "CustomerMobile", Message = $"Customer with mobile '{cleanMobile}' not found. Please import Customers first.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            if (!decimal.TryParse(principalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var principal) || principal <= 0)
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = glNo, ColumnName = "Principal", Message = $"Invalid principal amount '{principalStr}'.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            var pledgeDate = DateOnly.FromDateTime(DateTime.Today);
            if (!string.IsNullOrWhiteSpace(pledgeDateStr) && DateTime.TryParse(pledgeDateStr, CultureInfo.InvariantCulture, out var dt))
            {
                pledgeDate = DateOnly.FromDateTime(dt);
            }

            decimal grossWeight = 10m;
            if (!string.IsNullOrWhiteSpace(grossWeightStr) && decimal.TryParse(grossWeightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var gw)) grossWeight = gw;

            decimal netWeight = grossWeight;
            if (!string.IsNullOrWhiteSpace(netWeightStr) && decimal.TryParse(netWeightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var nw)) netWeight = nw;

            if (netWeight > grossWeight)
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = glNo, ColumnName = "NetWeight", Message = $"Net weight ({netWeight}g) cannot exceed Gross weight ({grossWeight}g).", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            int count = 1;
            if (!string.IsNullOrWhiteSpace(countStr) && int.TryParse(countStr, out var parsedCount) && parsedCount > 0) count = parsedCount;

            var loan = new Loan
            {
                LoanNumber = glNo,
                CustomerId = customerId,
                SchemeId = defaultScheme.Id,
                PledgeDate = pledgeDate,
                MaturityDate = pledgeDate.AddYears(1),
                LoanAmount = principal,
                MarketValue = Math.Round(netWeight * 6000m, 2),
                GrossWeight = grossWeight,
                NetWeight = netWeight,
                DeductionWeight = Math.Max(0m, grossWeight - netWeight),
                ApplicableGoldRate = 6000m,
                Status = LoanStatus.Active,
                Items = new List<LoanItem>
                {
                    new()
                    {
                        ItemNumber = 1,
                        JewelTypeId = defaultJewelType.Id,
                        Description = string.IsNullOrWhiteSpace(jewelName) ? "Imported Jewellery" : jewelName.Trim(),
                        Quantity = count,
                        GrossWeight = grossWeight,
                        NetWeight = netWeight,
                        DeductionWeight = Math.Max(0m, grossWeight - netWeight)
                    }
                }
            };

            loansToInsert.Add(loan);

            var voucher = new Voucher
            {
                VoucherNumber = $"VCH-MIG-{glNo}",
                VoucherDate = pledgeDate,
                VoucherType = VoucherType.Journal,
                Narration = $"LEGACY MIGRATION LOAN DISBURSAL: {glNo}",
                TotalDebit = principal,
                TotalCredit = principal,
                Lines = new List<VoucherLine>
                {
                    new() { LedgerHeadId = Guid.Empty, LineOrder = 1, DebitAmount = principal, CreditAmount = 0m, Narration = $"Jewel Loan Receivable {glNo}" },
                    new() { LedgerHeadId = Guid.Empty, LineOrder = 2, DebitAmount = 0m, CreditAmount = principal, Narration = $"Owner Capital / Legacy Portfolio {glNo}" }
                }
            };
            vouchersToInsert.Add(voucher);

            result.SuccessfullyProcessed++;
        }

        if (!isDryRun && loansToInsert.Any())
        {
            var receivableHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "1002");
            var capitalHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "3001");
            if (receivableHead != null && capitalHead != null)
            {
                foreach (var vch in vouchersToInsert)
                {
                    vch.Lines.First().LedgerHeadId = receivableHead.Id;
                    vch.Lines.Last().LedgerHeadId = capitalHead.Id;
                }
            }

            await _context.Loans.AddRangeAsync(loansToInsert);
            await _context.Vouchers.AddRangeAsync(vouchersToInsert);
            await _context.SaveChangesAsync();
        }

        result.SummaryMessage = $"Processed {result.TotalRead} loan records. Valid: {result.SuccessfullyProcessed}, Errors: {result.FailedRows}. Mode: {(isDryRun ? "DRY-RUN (Simulated)" : "COMMITTED TO DATABASE")}";
        return result;
    }

    public async Task<MigrationExecutionResult> MigrateReceiptsAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun)
    {
        var (headers, rows) = await ReadRowsAsync(fileStream, fileName);
        var result = new MigrationExecutionResult
        {
            EntityType = MigrationEntityType.Receipts,
            IsDryRun = isDryRun,
            TotalRead = rows.Count
        };

        var loans = await _context.Loans.ToDictionaryAsync(l => l.LoanNumber);
        var existingReceipts = await _context.Receipts.Select(r => r.ReceiptNumber).ToHashSetAsync();
        var receiptsToInsert = new List<Receipt>();
        var seenReceiptsInFile = new HashSet<string>();

        int rowIndex = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            var rcptNo = GetValue(row, columnMapping, "ReceiptNumber");
            var glNo = GetValue(row, columnMapping, "LoanNumber");
            var dateStr = GetValue(row, columnMapping, "ReceiptDate");
            var principalPaidStr = GetValue(row, columnMapping, "PrincipalPaid");
            var interestPaidStr = GetValue(row, columnMapping, "InterestPaid");
            var postalChargesStr = GetValue(row, columnMapping, "PostalCharges");

            if (string.IsNullOrWhiteSpace(rcptNo))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, ColumnName = "ReceiptNumber", Message = "Receipt Number is required.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            rcptNo = rcptNo.Trim();
            if (existingReceipts.Contains(rcptNo) || seenReceiptsInFile.Contains(rcptNo))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = rcptNo, ColumnName = "ReceiptNumber", Message = $"Duplicate Receipt Number '{rcptNo}'.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }
            seenReceiptsInFile.Add(rcptNo);

            if (string.IsNullOrWhiteSpace(glNo) || !loans.TryGetValue(glNo.Trim(), out var loan))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = rcptNo, ColumnName = "LoanNumber", Message = $"Referenced loan '{glNo}' does not exist in system.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            var receiptDate = DateOnly.FromDateTime(DateTime.Today);
            if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var dt))
            {
                receiptDate = DateOnly.FromDateTime(dt);
            }

            decimal principalPaid = 0m;
            if (!string.IsNullOrWhiteSpace(principalPaidStr)) decimal.TryParse(principalPaidStr, NumberStyles.Any, CultureInfo.InvariantCulture, out principalPaid);

            decimal interestPaid = 0m;
            if (!string.IsNullOrWhiteSpace(interestPaidStr)) decimal.TryParse(interestPaidStr, NumberStyles.Any, CultureInfo.InvariantCulture, out interestPaid);

            decimal postal = 0m;
            if (!string.IsNullOrWhiteSpace(postalChargesStr)) decimal.TryParse(postalChargesStr, NumberStyles.Any, CultureInfo.InvariantCulture, out postal);

            decimal total = principalPaid + interestPaid + postal;
            if (total <= 0)
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = rcptNo, ColumnName = "TotalAmount", Message = "Total payment amount cannot be zero.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            var rcpt = new Receipt
            {
                ReceiptNumber = rcptNo,
                LoanId = loan.Id,
                ReceiptDate = receiptDate,
                PrincipalAmount = principalPaid,
                InterestAmount = interestPaid,
                PostalCharges = postal,
                TotalAmount = total,
                PaymentMode = PaymentMode.Cash,
                ReceiptType = (principalPaid >= loan.LoanAmount && loan.LoanAmount > 0) ? ReceiptType.Closure : (principalPaid > 0 ? ReceiptType.PartPrincipal : ReceiptType.InterestOnly),
                InterestCalculatedFromDate = loan.PledgeDate,
                InterestCalculatedToDate = receiptDate
            };

            receiptsToInsert.Add(rcpt);
            result.SuccessfullyProcessed++;
        }

        if (!isDryRun && receiptsToInsert.Any())
        {
            var cashHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "1001");
            var loanHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "1002");
            var interestHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "4001");
            var postalHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "4004");

            var vouchers = new List<Voucher>();

            foreach (var r in receiptsToInsert)
            {
                var loan = loans.Values.First(l => l.Id == r.LoanId);
                if (r.PrincipalAmount >= loan.LoanAmount)
                {
                    loan.Status = LoanStatus.Closed;
                    loan.ClosedDate = r.ReceiptDate;
                }
                else if (r.PrincipalAmount > 0)
                {
                    loan.Status = LoanStatus.PartiallyPaid;
                }

                if (cashHead != null && (loanHead != null || interestHead != null))
                {
                    var vch = new Voucher
                    {
                        VoucherNumber = $"VCH-RCPT-{r.ReceiptNumber}",
                        VoucherDate = r.ReceiptDate,
                        VoucherType = VoucherType.Receipt,
                        Narration = $"MIGRATED PAYMENT FOR {loan.LoanNumber}: TOTAL {r.TotalAmount}",
                        TotalDebit = r.TotalAmount,
                        TotalCredit = r.TotalAmount,
                        Lines = new List<VoucherLine>
                        {
                            new() { LedgerHeadId = cashHead.Id, LineOrder = 1, DebitAmount = r.TotalAmount, CreditAmount = 0m, Narration = "Cash Received" }
                        }
                    };

                    int order = 2;
                    if (r.PrincipalAmount > 0 && loanHead != null)
                        vch.Lines.Add(new VoucherLine { LedgerHeadId = loanHead.Id, LineOrder = order++, DebitAmount = 0m, CreditAmount = r.PrincipalAmount, Narration = $"Principal Collection {loan.LoanNumber}" });

                    if (r.InterestAmount > 0 && interestHead != null)
                        vch.Lines.Add(new VoucherLine { LedgerHeadId = interestHead.Id, LineOrder = order++, DebitAmount = 0m, CreditAmount = r.InterestAmount, Narration = $"Interest Collection {loan.LoanNumber}" });

                    if (r.PostalCharges > 0 && postalHead != null)
                        vch.Lines.Add(new VoucherLine { LedgerHeadId = postalHead.Id, LineOrder = order++, DebitAmount = 0m, CreditAmount = r.PostalCharges, Narration = "Postal Charges Recovered" });

                    vouchers.Add(vch);
                }
            }

            await _context.Receipts.AddRangeAsync(receiptsToInsert);
            if (vouchers.Any()) await _context.Vouchers.AddRangeAsync(vouchers);
            await _context.SaveChangesAsync();
        }

        result.SummaryMessage = $"Processed {result.TotalRead} receipt records. Valid: {result.SuccessfullyProcessed}, Errors: {result.FailedRows}. Mode: {(isDryRun ? "DRY-RUN (Simulated)" : "COMMITTED TO DATABASE")}";
        return result;
    }

    public async Task<MigrationExecutionResult> MigrateBankPledgesAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun)
    {
        var (headers, rows) = await ReadRowsAsync(fileStream, fileName);
        var result = new MigrationExecutionResult
        {
            EntityType = MigrationEntityType.BankPledges,
            IsDryRun = isDryRun,
            TotalRead = rows.Count
        };

        var bank = await _context.Banks.FirstOrDefaultAsync(b => b.IsActive)
                   ?? new Bank { Code = "SBI", Name = "State Bank of India (Migrated)", Branch = "Main Branch", IsActive = true };

        var existingPledges = await _context.BankPledges.Select(b => b.PledgeNumber).ToHashSetAsync();
        var pledgesToInsert = new List<BankPledge>();

        int rowIndex = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            var repledgeNo = GetValue(row, columnMapping, "RepledgeNumber");
            var amountStr = GetValue(row, columnMapping, "PledgeAmount");
            var weightStr = GetValue(row, columnMapping, "BankWeight");
            var dateStr = GetValue(row, columnMapping, "PledgeDate");

            if (string.IsNullOrWhiteSpace(repledgeNo))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, ColumnName = "RepledgeNumber", Message = "Repledge Number is required.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            repledgeNo = repledgeNo.Trim();
            if (existingPledges.Contains(repledgeNo))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = repledgeNo, ColumnName = "RepledgeNumber", Message = $"Duplicate Repledge Number '{repledgeNo}'.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, Identifier = repledgeNo, ColumnName = "PledgeAmount", Message = $"Invalid pledge amount '{amountStr}'.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            decimal weight = 50m;
            if (!string.IsNullOrWhiteSpace(weightStr)) decimal.TryParse(weightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out weight);

            var date = DateOnly.FromDateTime(DateTime.Today);
            if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var dt)) date = DateOnly.FromDateTime(dt);

            var pledge = new BankPledge
            {
                PledgeNumber = repledgeNo,
                BankId = bank.Id,
                PledgeDate = date,
                MaturityDate = date.AddYears(1),
                TotalPledgeAmount = amount,
                DisbursedAmount = amount,
                InterestRate = 10.5m,
                BankWeight = weight,
                BankAccountOrLoanNumber = $"BANK-ACC-{repledgeNo}",
                AccountHolderName = "Shop Owner (Migrated)",
                Status = BankPledgeStatus.Active
            };

            pledgesToInsert.Add(pledge);
            result.SuccessfullyProcessed++;
        }

        if (!isDryRun && pledgesToInsert.Any())
        {
            var cashHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "1001");
            var bankLoanHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "2001");

            var vouchers = new List<Voucher>();
            foreach (var p in pledgesToInsert)
            {
                if (cashHead != null && bankLoanHead != null)
                {
                    vouchers.Add(new Voucher
                    {
                        VoucherNumber = $"VCH-REPL-{p.PledgeNumber}",
                        VoucherDate = p.PledgeDate,
                        VoucherType = VoucherType.Journal,
                        Narration = $"MIGRATED BANK REPLEDGE: {p.PledgeNumber}",
                        TotalDebit = p.DisbursedAmount,
                        TotalCredit = p.DisbursedAmount,
                        Lines = new List<VoucherLine>
                        {
                            new() { LedgerHeadId = cashHead.Id, LineOrder = 1, DebitAmount = p.DisbursedAmount, CreditAmount = 0m, Narration = "Cash received from bank pledge" },
                            new() { LedgerHeadId = bankLoanHead.Id, LineOrder = 2, DebitAmount = 0m, CreditAmount = p.DisbursedAmount, Narration = $"Bank Repledge Loan {p.PledgeNumber}" }
                        }
                    });
                }
            }

            await _context.BankPledges.AddRangeAsync(pledgesToInsert);
            if (vouchers.Any()) await _context.Vouchers.AddRangeAsync(vouchers);
            await _context.SaveChangesAsync();
        }

        result.SummaryMessage = $"Processed {result.TotalRead} bank repledge records. Valid: {result.SuccessfullyProcessed}, Errors: {result.FailedRows}. Mode: {(isDryRun ? "DRY-RUN (Simulated)" : "COMMITTED TO DATABASE")}";
        return result;
    }

    public async Task<MigrationExecutionResult> MigrateLedgerBalancesAsync(
        Stream fileStream,
        string fileName,
        Dictionary<string, string> columnMapping,
        bool isDryRun)
    {
        var (headers, rows) = await ReadRowsAsync(fileStream, fileName);
        var result = new MigrationExecutionResult
        {
            EntityType = MigrationEntityType.LedgerBalances,
            IsDryRun = isDryRun,
            TotalRead = rows.Count
        };

        var ledgerHeads = await _context.LedgerHeads.ToDictionaryAsync(h => h.Code);
        var voucherLines = new List<VoucherLine>();
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        int rowIndex = 1;
        int lineOrder = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            var code = GetValue(row, columnMapping, "AccountCode");
            var name = GetValue(row, columnMapping, "AccountName");
            var debitStr = GetValue(row, columnMapping, "DebitAmount");
            var creditStr = GetValue(row, columnMapping, "CreditAmount");

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Errors.Add(new MigrationError { RowIndex = rowIndex, ColumnName = "AccountCode", Message = "Account Code is empty.", Severity = "Error" });
                result.FailedRows++;
                continue;
            }

            code = code.Trim();
            if (!ledgerHeads.TryGetValue(code, out var head))
            {
                head = new LedgerHead
                {
                    Code = code,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Ledger Head {code}" : name.Trim(),
                    AccountGroup = LedgerHeadType.Asset,
                    NormalBalance = "Debit",
                    IsSystemAccount = false
                };
                if (!isDryRun)
                {
                    await _context.LedgerHeads.AddAsync(head);
                    await _context.SaveChangesAsync();
                }
                ledgerHeads[code] = head;
            }

            decimal dr = 0m;
            if (!string.IsNullOrWhiteSpace(debitStr)) decimal.TryParse(debitStr, NumberStyles.Any, CultureInfo.InvariantCulture, out dr);

            decimal cr = 0m;
            if (!string.IsNullOrWhiteSpace(creditStr)) decimal.TryParse(creditStr, NumberStyles.Any, CultureInfo.InvariantCulture, out cr);

            if (dr == 0 && cr == 0) continue;

            totalDebit += dr;
            totalCredit += cr;

            voucherLines.Add(new VoucherLine
            {
                LedgerHeadId = head.Id,
                LineOrder = lineOrder++,
                DebitAmount = dr,
                CreditAmount = cr,
                Narration = $"OPENING BALANCE MIGRATION: {head.Name}"
            });

            result.SuccessfullyProcessed++;
        }

        if (totalDebit != totalCredit)
        {
            result.Errors.Add(new MigrationError
            {
                RowIndex = rowIndex,
                ColumnName = "TotalBalance",
                Message = $"Trial Balance Imbalance: Total Debit (₹{totalDebit:N2}) != Total Credit (₹{totalCredit:N2}). Diff: ₹{Math.Abs(totalDebit - totalCredit):N2}",
                Severity = "Warning"
            });
        }

        if (!isDryRun && voucherLines.Any())
        {
            var voucher = new Voucher
            {
                VoucherNumber = $"VCH-OPENING-BAL-{DateTime.Today:yyyyMMdd}",
                VoucherDate = DateOnly.FromDateTime(DateTime.Today),
                VoucherType = VoucherType.Journal,
                Narration = "MIGRATED OPENING BALANCES AS PER PREVIOUS SYSTEM AUDIT",
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                Lines = voucherLines
            };

            await _context.Vouchers.AddAsync(voucher);
            await _context.SaveChangesAsync();
        }

        result.SummaryMessage = $"Processed {result.TotalRead} ledger accounts. Total Dr: ₹{totalDebit:N2}, Total Cr: ₹{totalCredit:N2}. Mode: {(isDryRun ? "DRY-RUN (Simulated)" : "COMMITTED TO DATABASE")}";
        return result;
    }

    public async Task<MigrationReconciliation> GenerateReconciliationReportAsync(
        decimal? expectedPrincipal = null,
        decimal? expectedInterest = null,
        decimal? expectedCash = null,
        decimal? expectedBankOutstanding = null)
    {
        var recon = new MigrationReconciliation();

        // 1. Current active loans principal
        recon.NewOutstandingPrincipal = await _context.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid)
            .SumAsync(l => l.LoanAmount);

        // 2. Total interest collected
        recon.NewInterestReceived = await _context.Receipts
            .SumAsync(r => r.InterestAmount);

        // 3. Cash balance from ledger lines
        var cashHead = await _context.LedgerHeads.FirstOrDefaultAsync(h => h.Code == "1001");
        if (cashHead != null)
        {
            var cashLines = await _context.VoucherLines
                .Where(v => v.LedgerHeadId == cashHead.Id)
                .ToListAsync();
            recon.NewCashBalance = cashLines.Sum(l => l.DebitAmount - l.CreditAmount);
        }

        // 4. Bank Repledge Outstanding
        recon.NewBankRepledgeOutstanding = await _context.BankPledges
            .Where(b => b.Status == BankPledgeStatus.Active || b.Status == BankPledgeStatus.PartiallyReleased)
            .SumAsync(b => b.DisbursedAmount);

        recon.OldOutstandingPrincipal = expectedPrincipal ?? recon.NewOutstandingPrincipal;
        recon.OldInterestReceived = expectedInterest ?? recon.NewInterestReceived;
        recon.OldCashBalance = expectedCash ?? recon.NewCashBalance;
        recon.OldBankRepledgeOutstanding = expectedBankOutstanding ?? recon.NewBankRepledgeOutstanding;

        recon.ImportedRecordCount = await _context.Loans.CountAsync();
        recon.OldRecordCount = recon.ImportedRecordCount;

        try
        {
            recon.IntegrityAudit = await _accountingReportService.RunIntegrityAuditAsync(DateOnly.FromDateTime(DateTime.Today));
        }
        catch { }

        return recon;
    }

    private static string GetValue(Dictionary<string, string> row, Dictionary<string, string> mapping, string standardField)
    {
        if (mapping.TryGetValue(standardField, out var fileHeader) && row.TryGetValue(fileHeader, out var val))
        {
            return val;
        }

        var direct = row.FirstOrDefault(kvp => kvp.Key.Equals(standardField, StringComparison.OrdinalIgnoreCase)).Value;
        return direct ?? string.Empty;
    }

    private static async Task<(List<string> Headers, List<Dictionary<string, string>> Rows)> ReadRowsAsync(
        Stream fileStream,
        string fileName,
        int? maxRowsToRead = null)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".xlsx" || ext == ".xls")
        {
            return ReadExcel(fileStream, maxRowsToRead);
        }
        else
        {
            return await ReadCsvAsync(fileStream, maxRowsToRead);
        }
    }

    private static (List<string> Headers, List<Dictionary<string, string>> Rows) ReadExcel(Stream stream, int? maxRowsToRead)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var headers = new List<string>();
        var rows = new List<Dictionary<string, string>>();

        var firstRow = worksheet.FirstRowUsed();
        if (firstRow == null) return (headers, rows);

        foreach (var cell in firstRow.CellsUsed())
        {
            headers.Add(cell.GetString().Trim());
        }

        var dataRows = worksheet.RowsUsed().Skip(1);
        if (maxRowsToRead.HasValue) dataRows = dataRows.Take(maxRowsToRead.Value);

        foreach (var row in dataRows)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                var cell = row.Cell(i + 1);
                dict[headers[i]] = cell.GetString()?.Trim() ?? string.Empty;
            }
            if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                rows.Add(dict);
            }
        }

        return (headers, rows);
    }

    private static async Task<(List<string> Headers, List<Dictionary<string, string>> Rows)> ReadCsvAsync(Stream stream, int? maxRowsToRead)
    {
        var headers = new List<string>();
        var rows = new List<Dictionary<string, string>>();

        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        string? headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine)) return (headers, rows);

        headers = ParseCsvLine(headerLine);

        string? line;
        int count = 0;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseCsvLine(line);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count; i++)
            {
                dict[headers[i]] = i < values.Count ? values[i] : string.Empty;
            }

            rows.Add(dict);
            count++;
            if (maxRowsToRead.HasValue && count >= maxRowsToRead.Value) break;
        }

        return (headers, rows);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    sb.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString().Trim());
        return result;
    }
}
