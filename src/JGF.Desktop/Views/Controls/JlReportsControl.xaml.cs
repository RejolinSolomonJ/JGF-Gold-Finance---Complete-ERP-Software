using System.IO;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Reports;
using JGF.Application.Services;
using JGF.Domain.Enums;
using Microsoft.Win32;

namespace JGF.Desktop.Views.Controls;

public partial class JlReportsControl : UserControl
{
    private readonly IJlReportService _reportService;
    private readonly ISchemeService _schemeService;
    private readonly ICustomerService _customerService;
    private readonly IExcelExportService _excelExportService;
    private readonly Guid? _currentUserId;
    private readonly UserRole _currentUserRole;

    private OutstandingReportSummaryDto? _lastOutstanding;
    private LoanSummaryReportDto? _lastSummary;
    private PledgeReportSummaryDto? _lastPledge;
    private ReleaseReportSummaryDto? _lastRelease;
    private JlCashReportSummaryDto? _lastCash;
    private AllStockReportSummaryDto? _lastAllStock;
    private JlStockReportSummaryDto? _lastJlStock;
    private PostalChargeReportSummaryDto? _lastPostal;
    private InterestCollectionReportSummaryDto? _lastInterest;
    private CustomerStatementReportDto? _lastStatement;
    private CancellationTokenSource? _loadCts;
    private bool _isLoading;

    public JlReportsControl(
        IJlReportService reportService,
        ISchemeService schemeService,
        ICustomerService customerService,
        IExcelExportService excelExportService,
        Guid? currentUserId = null,
        UserRole currentUserRole = UserRole.Owner)
    {
        InitializeComponent();
        _reportService = reportService;
        _schemeService = schemeService;
        _customerService = customerService;
        _excelExportService = excelExportService;
        _currentUserId = currentUserId;
        _currentUserRole = currentUserRole;

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSchemesAsync();
        await LoadReportDataAsync();
    }

    private void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
        if (PrgLoading != null) PrgLoading.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (BtnApplyFilter != null) BtnApplyFilter.IsEnabled = !isLoading;
        if (BtnExportExcel != null) BtnExportExcel.IsEnabled = !isLoading;
        if (BtnPrintPdf != null) BtnPrintPdf.IsEnabled = !isLoading;
    }

    private async Task LoadSchemesAsync()
    {
        try
        {
            var schemes = await _schemeService.GetAllSchemesAsync();
            CmbSchemeFilter.ItemsSource = schemes;
        }
        catch { }
    }

    private void OnReportSelectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelOutstanding == null || PanelSummary == null || PanelPledge == null || PanelRelease == null || PanelJlCash == null || PanelAllStock == null || PanelJlStock == null || PanelPostal == null || PanelInterest == null || PanelStatement == null || BtnNewPostalCharge == null || CmbReportSelect == null) return;

        PanelOutstanding.Visibility = Visibility.Collapsed;
        PanelSummary.Visibility = Visibility.Collapsed;
        PanelPledge.Visibility = Visibility.Collapsed;
        PanelRelease.Visibility = Visibility.Collapsed;
        PanelJlCash.Visibility = Visibility.Collapsed;
        PanelAllStock.Visibility = Visibility.Collapsed;
        PanelJlStock.Visibility = Visibility.Collapsed;
        PanelPostal.Visibility = Visibility.Collapsed;
        PanelInterest.Visibility = Visibility.Collapsed;
        PanelStatement.Visibility = Visibility.Collapsed;

        BtnNewPostalCharge.Visibility = Visibility.Collapsed;

        switch (CmbReportSelect.SelectedIndex)
        {
            case 0: PanelOutstanding.Visibility = Visibility.Visible; break;
            case 1: PanelSummary.Visibility = Visibility.Visible; break;
            case 2: PanelPledge.Visibility = Visibility.Visible; break;
            case 3: PanelRelease.Visibility = Visibility.Visible; break;
            case 4: PanelJlCash.Visibility = Visibility.Visible; break;
            case 5: PanelAllStock.Visibility = Visibility.Visible; break;
            case 6: PanelJlStock.Visibility = Visibility.Visible; break;
            case 7:
                PanelPostal.Visibility = Visibility.Visible;
                BtnNewPostalCharge.Visibility = Visibility.Visible;
                break;
            case 8: PanelInterest.Visibility = Visibility.Visible; break;
            case 9: PanelStatement.Visibility = Visibility.Visible; break;
        }

        _ = LoadReportDataAsync();
    }

    private JlReportFilterDto BuildFilter()
    {
        DateOnly? from = DpFromDate.SelectedDate.HasValue 
            ? DateOnly.FromDateTime(DpFromDate.SelectedDate.Value) : null;
        DateOnly? to = DpToDate.SelectedDate.HasValue 
            ? DateOnly.FromDateTime(DpToDate.SelectedDate.Value) : null;

        Guid? schemeId = CmbSchemeFilter.SelectedValue as Guid?;

        LoanStatus? status = null;
        if (CmbStatusFilter.SelectedItem is ComboBoxItem cbiStatus && cbiStatus.Content.ToString() != "All")
        {
            if (Enum.TryParse<LoanStatus>(cbiStatus.Content.ToString(), out var parsedStatus))
                status = parsedStatus;
        }

        string? location = null;
        if (CmbLocationFilter.SelectedItem is ComboBoxItem cbiLoc && cbiLoc.Content.ToString() != "All")
        {
            location = cbiLoc.Content.ToString();
        }

        string? search = string.IsNullOrWhiteSpace(TxtSearchFilter.Text) ? null : TxtSearchFilter.Text.Trim();

        return new JlReportFilterDto(from, to, schemeId, null, status, search, location);
    }

    private async Task LoadReportDataAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        var filter = BuildFilter();

        try
        {
            SetLoading(true);
            switch (CmbReportSelect.SelectedIndex)
            {
                case 0: // Outstanding
                    _lastOutstanding = await _reportService.GetOutstandingReportAsync(filter, ct);
                    GridOutstanding.ItemsSource = _lastOutstanding.Items;
                    TxtOutstandingCount.Text = $"Total Loans: {_lastOutstanding.TotalLoans}";
                    TxtOutstandingNetWt.Text = $"{_lastOutstanding.TotalNetWeight:F3} g";
                    TxtOutstandingPrincipal.Text = $"₹ {_lastOutstanding.TotalOutstandingPrincipal:N2}";
                    TxtOutstandingPayable.Text = $"₹ {_lastOutstanding.TotalPayable:N2}";
                    break;

                case 1: // Summary
                    _lastSummary = await _reportService.GetLoanSummaryReportAsync(filter, ct);
                    GridSummary.ItemsSource = _lastSummary.Items;
                    TxtSummaryTotals.Text = $"Schemes: {_lastSummary.TotalSchemes} | Total Loans: {_lastSummary.GrandTotalLoans}";
                    TxtSummaryDisbursed.Text = $"₹ {_lastSummary.GrandDisbursed:N2}";
                    TxtSummaryOutstanding.Text = $"₹ {_lastSummary.GrandOutstandingPrincipal:N2}";
                    TxtSummaryNetWt.Text = $"{_lastSummary.GrandNetWeight:F3} g";
                    break;

                case 2: // Pledge
                    _lastPledge = await _reportService.GetPledgeReportAsync(filter, ct);
                    GridPledge.ItemsSource = _lastPledge.Items;
                    TxtPledgeCount.Text = $"Total Loans: {_lastPledge.TotalLoans} ({_lastPledge.TotalItems} items)";
                    TxtPledgeGross.Text = $"{_lastPledge.TotalGrossWeight:F3} g";
                    TxtPledgeNet.Text = $"{_lastPledge.TotalNetWeight:F3} g";
                    TxtPledgeTotalDisbursed.Text = $"₹ {_lastPledge.TotalLoanAmount:N2}";
                    break;

                case 3: // Release
                    _lastRelease = await _reportService.GetReleaseReportAsync(filter, ct);
                    GridRelease.ItemsSource = _lastRelease.Items;
                    TxtReleaseCount.Text = $"Closed Loans: {_lastRelease.TotalClosedLoans}";
                    TxtReleasePrincipal.Text = $"₹ {_lastRelease.TotalPrincipalPaid:N2}";
                    TxtReleaseInterest.Text = $"₹ {_lastRelease.TotalInterestPaid + _lastRelease.TotalPenalPaid:N2}";
                    TxtReleaseTotal.Text = $"₹ {_lastRelease.GrandTotalAmountPaid:N2}";
                    break;

                case 4: // JL Cash
                    var dtFrom = filter.FromDate ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
                    var dtTo = filter.ToDate ?? DateOnly.FromDateTime(DateTime.Today);
                    _lastCash = await _reportService.GetJlCashReportAsync(dtFrom, dtTo);
                    GridJlCash.ItemsSource = _lastCash.Items;
                    TxtCashOutflow.Text = $"₹ {_lastCash.TotalDisbursedAmount:N2}";
                    TxtCashInflow.Text = $"₹ {_lastCash.GrandTotalInflow:N2}";
                    TxtCashNet.Text = $"₹ {_lastCash.NetOverallCashMovement:N2}";
                    break;

                case 5: // All Stock
                    _lastAllStock = await _reportService.GetAllStockReportAsync(filter);
                    GridAllStock.ItemsSource = _lastAllStock.Items;
                    TxtAllStockTotals.Text = $"Categories: {_lastAllStock.TotalJewelCategories}";
                    TxtStockPieces.Text = $"{_lastAllStock.GrandTotalPieces}";
                    TxtStockLockerNet.Text = $"{_lastAllStock.GrandShopLockerNetWeight:F3} g";
                    TxtStockBankNet.Text = $"{_lastAllStock.GrandBankCustodyNetWeight:F3} g";
                    TxtStockGrandNet.Text = $"{_lastAllStock.GrandNetWeight:F3} g";
                    break;

                case 6: // JL Stock
                    _lastJlStock = await _reportService.GetJlStockReportAsync(filter);
                    GridJlStock.ItemsSource = _lastJlStock.Items;
                    TxtJlStockCount.Text = $"Total Items: {_lastJlStock.TotalItemsCount} ({_lastJlStock.TotalPieces} pcs)";
                    TxtJlStockGross.Text = $"{_lastJlStock.TotalGrossWeight:F3} g";
                    TxtJlStockNet.Text = $"{_lastJlStock.TotalNetWeight:F3} g";
                    TxtJlStockValue.Text = $"₹ {_lastJlStock.TotalAppraisedValue:N2}";
                    break;

                case 7: // Postal
                    _lastPostal = await _reportService.GetPostalChargeReportAsync(filter);
                    GridPostal.ItemsSource = _lastPostal.Items;
                    TxtPostalCount.Text = $"Total Entries: {_lastPostal.TotalEntries}";
                    TxtPostalTotal.Text = $"₹ {_lastPostal.TotalPostalAmount:N2}";
                    break;

                case 8: // Interest Collection
                    _lastInterest = await _reportService.GetInterestCollectionReportAsync(filter);
                    GridInterest.ItemsSource = _lastInterest.Items;
                    TxtInterestCount.Text = $"Total Receipts: {_lastInterest.TotalReceiptsCount}";
                    TxtInterestRegular.Text = $"₹ {_lastInterest.TotalInterestAmount:N2}";
                    TxtInterestPenal.Text = $"₹ {_lastInterest.TotalPenalInterestAmount:N2}";
                    TxtInterestGrandTotal.Text = $"₹ {_lastInterest.GrandTotalInterestCollected:N2}";
                    break;

                case 9: // Customer Statement
                    var custSearch = filter.SearchText;
                    if (string.IsNullOrWhiteSpace(custSearch))
                    {
                        var customers = await _customerService.SearchCustomersAsync("", 1);
                        if (customers.Count > 0)
                        {
                            await LoadCustomerStatement(customers[0].Id);
                        }
                    }
                    else
                    {
                        var customers = await _customerService.SearchCustomersAsync(custSearch, 1);
                        if (customers.Count > 0)
                        {
                            await LoadCustomerStatement(customers[0].Id);
                        }
                    }
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled due to user switching report / filter
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to generate report: {ex.Message}", "Report Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadCustomerStatement(Guid customerId)
    {
        _lastStatement = await _reportService.GetCustomerStatementAsync(customerId);
        TxtCustName.Text = $"Customer: {_lastStatement.FullName} ({_lastStatement.CustomerCode})";
        TxtCustPhone.Text = $"Mobile: {_lastStatement.MobileNumber}";
        TxtCustAddress.Text = $"Address: {_lastStatement.Address ?? "-"}, {_lastStatement.City ?? "-"}";

        TxtCustLoansStats.Text = $"Total Loans: {_lastStatement.TotalLoansTaken} (Active: {_lastStatement.ActiveLoansCount}, Closed: {_lastStatement.ClosedLoansCount})";
        TxtCustOutstanding.Text = $"Principal Outstanding: ₹ {_lastStatement.CurrentOutstandingPrincipal:N2}";
        TxtCustAccrued.Text = $"Accrued Interest: ₹ {_lastStatement.CurrentAccruedInterest:N2}";
        TxtCustTotalPayable.Text = $"₹ {_lastStatement.TotalPayableNow:N2}";

        GridStatement.ItemsSource = _lastStatement.Transactions;
    }

    private async void OnFilterApplyClick(object sender, RoutedEventArgs e) => await LoadReportDataAsync();

    private async void OnFilterResetClick(object sender, RoutedEventArgs e)
    {
        DpFromDate.SelectedDate = null;
        DpToDate.SelectedDate = null;
        CmbSchemeFilter.SelectedIndex = -1;
        CmbStatusFilter.SelectedIndex = 0;
        CmbLocationFilter.SelectedIndex = 0;
        TxtSearchFilter.Text = string.Empty;

        await LoadReportDataAsync();
    }

    private async void OnNewPostalChargeClick(object sender, RoutedEventArgs e)
    {
        // Simple input dialog for postal charge entry
        var loanPrompt = Microsoft.VisualBasic.Interaction.InputBox("Enter GL Loan Number for Postal Charge:", "Postal Charge Entry", "");
        if (string.IsNullOrWhiteSpace(loanPrompt)) return;

        var amountPrompt = Microsoft.VisualBasic.Interaction.InputBox("Enter Postal Amount (₹):", "Postal Charge Entry", "50.00");
        if (!decimal.TryParse(amountPrompt, out var amount) || amount <= 0)
        {
            MessageBox.Show("Please enter a valid postal amount.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reason = Microsoft.VisualBasic.Interaction.InputBox("Enter Narration / Registered Notice Details:", "Postal Charge Entry", "Overdue Notice Postage Recovery");

        try
        {
            // Find loan
            var customers = await _customerService.SearchCustomersAsync(loanPrompt.Trim(), 1);
            var filter = new JlReportFilterDto(SearchText: loanPrompt.Trim());
            var outReport = await _reportService.GetOutstandingReportAsync(filter);
            var loan = outReport.Items.FirstOrDefault();

            if (loan == null)
            {
                MessageBox.Show($"Loan '{loanPrompt}' not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dto = new RecordPostalChargeDto(
                loan.LoanId,
                amount,
                DateOnly.FromDateTime(DateTime.Today),
                reason,
                PaymentMode.Cash,
                _currentUserId ?? Guid.Empty,
                _currentUserRole
            );

            var vNumber = await _reportService.RecordPostalChargeAsync(dto);
            MessageBox.Show($"Postal charge recorded successfully!\nVoucher Number: {vNumber}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadReportDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error recording postal charge: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExportExcelClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string title = "Report";
            string subtitle = "JGF Jewellers Finance";
            var cols = new List<ExcelColumnDefinition>();
            var rows = new List<List<object?>>();
            List<object?>? totals = null;

            switch (CmbReportSelect.SelectedIndex)
            {
                case 0: // Outstanding
                    if (_lastOutstanding == null || _lastOutstanding.Items.Count == 0) return;
                    title = "Jewel Loan Outstanding Report (நிலுவை கடன்)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("GL #", 12),
                        new("Pledge Date", 14, "dd-MMM-yyyy"),
                        new("Customer Name", 22),
                        new("Phone", 14),
                        new("Scheme", 15),
                        new("Gross Wt (g)", 14, "0.000", true),
                        new("Net Wt (g)", 14, "0.000", true),
                        new("Principal Bal (₹)", 18, "₹ #,##0.00", true),
                        new("Days", 8, "#,##0", true),
                        new("Accrued Int (₹)", 16, "₹ #,##0.00", true),
                        new("Total Payable (₹)", 18, "₹ #,##0.00", true),
                        new("Status", 12),
                        new("Location", 18)
                    };
                    rows = _lastOutstanding.Items.Select(i => new List<object?>
                    {
                        i.GlNumber, i.PledgeDate, i.CustomerName, i.CustomerPhone, i.SchemeName,
                        i.GrossWeight, i.NetWeight, i.PrincipalBalance, i.DaysElapsed, i.AccruedInterest,
                        i.TotalPayable, i.Status.ToString(), i.StorageLocation
                    }).ToList();
                    totals = new List<object?>
                    {
                        "TOTALS", null, null, null, $"{_lastOutstanding.TotalLoans} Loans",
                        _lastOutstanding.TotalGrossWeight, _lastOutstanding.TotalNetWeight,
                        _lastOutstanding.TotalOutstandingPrincipal, null, _lastOutstanding.TotalAccruedInterest,
                        _lastOutstanding.TotalPayable, null, null
                    };
                    break;

                case 1: // Summary
                    if (_lastSummary == null) return;
                    title = "Scheme-Wise Loan Summary Report (சுருக்க அறிக்கை)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("Code", 10), new("Scheme Name", 22), new("Total Loans", 12, "#,##0", true),
                        new("Active", 10, "#,##0", true), new("Closed", 10, "#,##0", true),
                        new("Disbursed (₹)", 18, "₹ #,##0.00", true), new("Repaid (₹)", 18, "₹ #,##0.00", true),
                        new("Outstanding (₹)", 18, "₹ #,##0.00", true), new("Net Wt (g)", 14, "0.000", true),
                        new("Interest Collected (₹)", 20, "₹ #,##0.00", true)
                    };
                    rows = _lastSummary.Items.Select(i => new List<object?>
                    {
                        i.SchemeCode, i.SchemeName, i.TotalLoansCount, i.ActiveLoansCount, i.ClosedLoansCount,
                        i.TotalDisbursedAmount, i.TotalPrincipalCollected, i.TotalOutstandingPrincipal,
                        i.TotalNetWeight, i.TotalInterestCollected
                    }).ToList();
                    totals = new List<object?>
                    {
                        "GRAND TOTALS", null, _lastSummary.GrandTotalLoans, _lastSummary.GrandTotalActive,
                        _lastSummary.GrandTotalClosed, _lastSummary.GrandDisbursed, _lastSummary.GrandPrincipalCollected,
                        _lastSummary.GrandOutstandingPrincipal, _lastSummary.GrandNetWeight, _lastSummary.GrandInterestCollected
                    };
                    break;

                case 2: // Pledge
                    if (_lastPledge == null) return;
                    title = "Pledge Loans Report (அடகு அறிக்கை)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("GL #", 12), new("Pledge Date", 14, "dd-MMM-yyyy"), new("Customer Name", 22),
                        new("Phone", 14), new("Scheme", 15), new("Items", 8, "#,##0", true),
                        new("Gross Wt (g)", 14, "0.000", true), new("Net Wt (g)", 14, "0.000", true),
                        new("Loan Amount (₹)", 18, "₹ #,##0.00", true), new("Rate %", 10, "0.00%", true)
                    };
                    rows = _lastPledge.Items.Select(i => new List<object?>
                    {
                        i.GlNumber, i.PledgeDate, i.CustomerName, i.CustomerPhone, i.SchemeName,
                        i.ItemCount, i.GrossWeight, i.NetWeight, i.LoanAmount, i.MonthlyInterestRate
                    }).ToList();
                    totals = new List<object?>
                    {
                        "TOTALS", null, null, null, $"{_lastPledge.TotalLoans} Loans",
                        _lastPledge.TotalItems, _lastPledge.TotalGrossWeight, _lastPledge.TotalNetWeight,
                        _lastPledge.TotalLoanAmount, null
                    };
                    break;

                case 3: // Release
                    if (_lastRelease == null) return;
                    title = "Release / Redemption Report (மீட்பு அறிக்கை)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("Receipt #", 14), new("Release Date", 14, "dd-MMM-yyyy"), new("GL #", 12),
                        new("Customer Name", 20), new("Scheme", 15), new("Principal (₹)", 16, "₹ #,##0.00", true),
                        new("Interest (₹)", 14, "₹ #,##0.00", true), new("Penal (₹)", 12, "₹ #,##0.00", true),
                        new("Total Paid (₹)", 18, "₹ #,##0.00", true), new("Mode", 10)
                    };
                    rows = _lastRelease.Items.Select(i => new List<object?>
                    {
                        i.ReceiptNumber, i.ReleaseDate, i.GlNumber, i.CustomerName, i.SchemeName,
                        i.PrincipalPaid, i.InterestPaid, i.PenalPaid, i.TotalAmountPaid, i.PaymentMode.ToString()
                    }).ToList();
                    totals = new List<object?>
                    {
                        "TOTALS", null, null, null, $"{_lastRelease.TotalClosedLoans} Loans",
                        _lastRelease.TotalPrincipalPaid, _lastRelease.TotalInterestPaid, _lastRelease.TotalPenalPaid,
                        _lastRelease.GrandTotalAmountPaid, null
                    };
                    break;

                case 4: // JL Cash
                    if (_lastCash == null) return;
                    title = "JL Cash Flow Report (நகை கடன் ரொக்க அறிக்கை)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("Date", 14, "dd-MMM-yyyy"), new("Disbursed #", 12, "#,##0", true),
                        new("Disbursed (₹)", 18, "₹ #,##0.00", true), new("Receipts #", 12, "#,##0", true),
                        new("Principal In (₹)", 18, "₹ #,##0.00", true), new("Interest In (₹)", 16, "₹ #,##0.00", true),
                        new("Total Inflow (₹)", 18, "₹ #,##0.00", true), new("Net Flow (₹)", 18, "₹ #,##0.00", true)
                    };
                    rows = _lastCash.Items.Select(i => new List<object?>
                    {
                        i.Date, i.LoansDisbursedCount, i.LoansDisbursedAmount, i.ReceiptsCount,
                        i.PrincipalCollectedAmount, i.InterestCollectedAmount, i.TotalInflow, i.NetCashMovement
                    }).ToList();
                    totals = new List<object?>
                    {
                        "TOTALS", _lastCash.TotalDisbursedCount, _lastCash.TotalDisbursedAmount,
                        _lastCash.TotalReceiptsCount, _lastCash.TotalPrincipalCollected,
                        _lastCash.TotalInterestCollected, _lastCash.GrandTotalInflow, _lastCash.NetOverallCashMovement
                    };
                    break;

                case 5: // All Stock
                    if (_lastAllStock == null) return;
                    title = "All Stock Summary Report (மொத்த நகை இருப்பு)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("Code", 10), new("Jewel Category", 20), new("Pieces", 10, "#,##0", true),
                        new("Gross Wt (g)", 14, "0.000", true), new("Deduction (g)", 14, "0.000", true),
                        new("Net Wt (g)", 14, "0.000", true), new("Locker Net (g)", 14, "0.000", true),
                        new("Bank Net (g)", 14, "0.000", true)
                    };
                    rows = _lastAllStock.Items.Select(i => new List<object?>
                    {
                        i.JewelTypeCode, i.JewelTypeName, i.TotalPieces, i.TotalGrossWeight,
                        i.TotalDeductionWeight, i.TotalNetWeight, i.ShopLockerNetWeight, i.BankCustodyNetWeight
                    }).ToList();
                    totals = new List<object?>
                    {
                        "GRAND TOTAL", null, _lastAllStock.GrandTotalPieces, _lastAllStock.GrandGrossWeight,
                        _lastAllStock.GrandDeductionWeight, _lastAllStock.GrandNetWeight,
                        _lastAllStock.GrandShopLockerNetWeight, _lastAllStock.GrandBankCustodyNetWeight
                    };
                    break;

                case 6: // JL Stock
                    if (_lastJlStock == null) return;
                    title = "JL Granular Stock Inventory (பொருள் இருப்பு)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("GL #", 12), new("Packet #", 10), new("Pledge Date", 14, "dd-MMM-yyyy"),
                        new("Customer", 20), new("Description", 22), new("Category", 14),
                        new("Qty", 8, "#,##0", true), new("Gross (g)", 12, "0.000", true),
                        new("Net (g)", 12, "0.000", true), new("Appraised (₹)", 16, "₹ #,##0.00", true),
                        new("Location", 18)
                    };
                    rows = _lastJlStock.Items.Select(i => new List<object?>
                    {
                        i.GlNumber, i.PacketNumber, i.PledgeDate, i.CustomerName, i.Description,
                        i.JewelTypeName, i.Quantity, i.GrossWeight, i.NetWeight, i.AppraisedValue, i.StorageLocation
                    }).ToList();
                    totals = new List<object?>
                    {
                        "TOTALS", null, null, null, $"{_lastJlStock.TotalItemsCount} Items",
                        null, _lastJlStock.TotalPieces, _lastJlStock.TotalGrossWeight,
                        _lastJlStock.TotalNetWeight, _lastJlStock.TotalAppraisedValue, null
                    };
                    break;

                case 7: // Postal
                    if (_lastPostal == null) return;
                    title = "Postal Charges Report (அஞ்சல் கட்டணம்)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("Date", 14, "dd-MMM-yyyy"), new("Voucher #", 14), new("GL #", 12),
                        new("Customer Name", 22), new("Phone", 14), new("Amount (₹)", 16, "₹ #,##0.00", true),
                        new("Narration", 30)
                    };
                    rows = _lastPostal.Items.Select(i => new List<object?>
                    {
                        i.Date, i.VoucherNumber, i.GlNumber, i.CustomerName, i.CustomerPhone, i.Amount, i.Narration
                    }).ToList();
                    totals = new List<object?>
                    {
                        "TOTAL", null, null, null, $"{_lastPostal.TotalEntries} Entries", _lastPostal.TotalPostalAmount, null
                    };
                    break;

                case 8: // Interest Collection
                    if (_lastInterest == null) return;
                    title = "Interest Collection Audit Report (வட்டி வசூல்)";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("Receipt #", 14), new("Date", 14, "dd-MMM-yyyy"), new("GL #", 12),
                        new("Customer Name", 20), new("Scheme", 14), new("Days", 8, "#,##0", true),
                        new("Interest (₹)", 16, "₹ #,##0.00", true), new("Penal (₹)", 14, "₹ #,##0.00", true),
                        new("Total Collected (₹)", 18, "₹ #,##0.00", true), new("Mode", 10)
                    };
                    rows = _lastInterest.Items.Select(i => new List<object?>
                    {
                        i.ReceiptNumber, i.ReceiptDate, i.GlNumber, i.CustomerName, i.SchemeName,
                        i.Days, i.InterestAmount, i.PenalInterestAmount, i.TotalInterestCollected, i.PaymentMode.ToString()
                    }).ToList();
                    totals = new List<object?>
                    {
                        "TOTAL", null, null, null, $"{_lastInterest.TotalReceiptsCount} Receipts",
                        null, _lastInterest.TotalInterestAmount, _lastInterest.TotalPenalInterestAmount,
                        _lastInterest.GrandTotalInterestCollected, null
                    };
                    break;

                case 9: // Customer Statement
                    if (_lastStatement == null) return;
                    title = $"Customer Statement — {_lastStatement.FullName} ({_lastStatement.CustomerCode})";
                    cols = new List<ExcelColumnDefinition>
                    {
                        new("Date", 14, "dd-MMM-yyyy"), new("Type", 16), new("Ref #", 14),
                        new("Particulars", 30), new("Debit (₹)", 16, "₹ #,##0.00", true),
                        new("Credit (₹)", 16, "₹ #,##0.00", true), new("Principal Bal (₹)", 18, "₹ #,##0.00", true)
                    };
                    rows = _lastStatement.Transactions.Select(t => new List<object?>
                    {
                        t.Date, t.TransactionType, t.ReferenceNumber, t.Particulars, t.DebitAmount, t.CreditAmount, t.RunningPrincipalBalance
                    }).ToList();
                    break;
            }

            var excelBytes = _excelExportService.ExportToExcel(title, subtitle, cols, rows, totals);

            var sfd = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"JGF_{title.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                File.WriteAllBytes(sfd.FileName, excelBytes);
                MessageBox.Show($"Report exported successfully to:\n{sfd.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export Excel: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnExportFullDbClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"JGF_Full_Database_Export_{DateTime.Today:yyyyMMdd}.xlsx",
                Title = "Export Complete JGF Database to Excel"
            };

            if (sfd.ShowDialog() == true)
            {
                var bytes = await _excelExportService.ExportFullDatabaseToExcelAsync();
                await File.WriteAllBytesAsync(sfd.FileName, bytes);

                var res = MessageBox.Show(
                    $"Complete database exported successfully to Excel!\n\nFile: {sfd.FileName}\n\nWould you like to open it now?",
                    "Export Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Full database Excel export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnPrintPdfClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                // Print the currently active grid
                UIElement? printTarget = null;
                switch (CmbReportSelect.SelectedIndex)
                {
                    case 0: printTarget = GridOutstanding; break;
                    case 1: printTarget = GridSummary; break;
                    case 2: printTarget = GridPledge; break;
                    case 3: printTarget = GridRelease; break;
                    case 4: printTarget = GridJlCash; break;
                    case 5: printTarget = GridAllStock; break;
                    case 6: printTarget = GridJlStock; break;
                    case 7: printTarget = GridPostal; break;
                    case 8: printTarget = GridInterest; break;
                    case 9: printTarget = PanelStatement; break;
                }

                if (printTarget != null)
                {
                    printDlg.PrintVisual(printTarget, $"JGF Report {CmbReportSelect.Text}");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

