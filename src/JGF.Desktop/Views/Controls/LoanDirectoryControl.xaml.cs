using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JGF.Application.Loans;
using JGF.Application.Receipts;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Serilog;

namespace JGF.Desktop.Views.Controls;

public class LoanDirectoryItemViewModel
{
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public string CustomerDisplayName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public DateOnly PledgeDate { get; set; }
    public DateOnly MaturityDate { get; set; }
    public string SchemeInfo { get; set; } = string.Empty;
    public string JewelDetails { get; set; } = string.Empty;
    public decimal DisbursedAmount { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal RemainingPrincipal { get; set; }
    public LoanStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public Brush BadgeBackground { get; set; } = Brushes.LightGray;
    public Brush BadgeForeground { get; set; } = Brushes.Black;
    public string ClosedOrLastPaidText { get; set; } = string.Empty;
    public string PacketNumber { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public bool IsOpen { get; set; }
}

public partial class LoanDirectoryControl : UserControl
{
    private readonly IDbContextFactory<JgfDbContext> _contextFactory;
    private readonly ILoanService _loanService;
    private readonly IReceiptService _receiptService;
    private readonly ISchemeService _schemeService;
    private readonly User? _currentUser;

    private bool _isInitialized = false;
    private List<LoanDirectoryItemViewModel> _allItems = new();
    private List<Scheme> _schemes = new();

    public LoanDirectoryControl(
        IDbContextFactory<JgfDbContext> contextFactory,
        ILoanService loanService,
        IReceiptService receiptService,
        ISchemeService schemeService,
        User? currentUser = null)
    {
        InitializeComponent();
        _contextFactory = contextFactory;
        _loanService = loanService;
        _receiptService = receiptService;
        _schemeService = schemeService;
        _currentUser = currentUser;

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            await LoadSchemesAsync();
            await LoadLoanDirectoryAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Loan Directory Control.");
            MessageBox.Show($"Failed to load loan directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadSchemesAsync()
    {
        try
        {
            _schemes = await _schemeService.GetAllSchemesAsync();
            CmbSchemeFilter.Items.Clear();
            CmbSchemeFilter.Items.Add(new ComboBoxItem { Content = "All Schemes (அனைத்தும்)", Tag = null });

            foreach (var s in _schemes)
            {
                CmbSchemeFilter.Items.Add(new ComboBoxItem
                {
                    Content = $"{s.Name} ({s.InterestRatePerMonth:F2}%)",
                    Tag = s.Id
                });
            }
            CmbSchemeFilter.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load schemes for filter.");
        }
    }

    public async Task LoadLoanDirectoryAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = context.Loans
                .AsNoTracking()
                .Include(l => l.Customer)
                .Include(l => l.Scheme)
                .Include(l => l.Items).ThenInclude(i => i.JewelType)
                .Include(l => l.Receipts)
                .Where(l => !l.IsDeleted);

            // 1. Text Search Filter
            var search = TxtSearchQuery.Text.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l =>
                    l.LoanNumber.ToLower().Contains(search) ||
                    l.Customer.Name.ToLower().Contains(search) ||
                    l.Customer.Phone.Contains(search) ||
                    l.Customer.Code.ToLower().Contains(search) ||
                    (l.PacketNumber != null && l.PacketNumber.ToLower().Contains(search)));
            }

            // 2. Scheme Filter
            if (CmbSchemeFilter.SelectedItem is ComboBoxItem schemeItem && schemeItem.Tag is Guid schemeId)
            {
                query = query.Where(l => l.SchemeId == schemeId);
            }

            // 3. Date Range Filter
            if (DpFromDate.SelectedDate.HasValue)
            {
                var fromDate = DateOnly.FromDateTime(DpFromDate.SelectedDate.Value);
                query = query.Where(l => l.PledgeDate >= fromDate);
            }
            if (DpToDate.SelectedDate.HasValue)
            {
                var toDate = DateOnly.FromDateTime(DpToDate.SelectedDate.Value);
                query = query.Where(l => l.PledgeDate <= toDate);
            }

            // 4. Status Filter
            var statusIndex = CmbStatusFilter.SelectedIndex;
            switch (statusIndex)
            {
                case 1: // Active Only
                    query = query.Where(l => l.Status == LoanStatus.Active);
                    break;
                case 2: // Partially Paid
                    query = query.Where(l => l.Status == LoanStatus.PartiallyPaid);
                    break;
                case 3: // Completely Closed
                    query = query.Where(l => l.Status == LoanStatus.Closed);
                    break;
                case 4: // Overdue
                    query = query.Where(l => (l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid) && l.MaturityDate < today);
                    break;
                case 5: // Auctioned
                    query = query.Where(l => l.Status == LoanStatus.Auctioned);
                    break;
            }

            var loans = await query
                .OrderByDescending(l => l.PledgeDate)
                .ThenByDescending(l => l.LoanNumber)
                .ToListAsync();

            // Transform into ViewModels
            var viewModels = new List<LoanDirectoryItemViewModel>();
            foreach (var loan in loans)
            {
                var principalPaid = loan.Receipts
                    .Where(r => !r.IsDeleted)
                    .Sum(r => r.PrincipalAmount);

                var remainingPrincipal = loan.Status == LoanStatus.Closed
                    ? 0m
                    : Math.Max(0m, loan.LoanAmount - principalPaid);

                var isOverdue = (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.PartiallyPaid) && loan.MaturityDate < today;

                string statusText;
                Brush bgBrush;
                Brush fgBrush;

                if (loan.Status == LoanStatus.Closed)
                {
                    statusText = "Closed (அடைப்பு)";
                    bgBrush = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Light Green
                    fgBrush = new SolidColorBrush(Color.FromRgb(22, 101, 52));   // Dark Green
                }
                else if (isOverdue)
                {
                    statusText = "Overdue (தவணை தவறியவை)";
                    bgBrush = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Light Red
                    fgBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27));   // Dark Red
                }
                else if (loan.Status == LoanStatus.PartiallyPaid)
                {
                    statusText = "Partially Paid (பகுதி)";
                    bgBrush = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Light Amber
                    fgBrush = new SolidColorBrush(Color.FromRgb(180, 83, 9));    // Dark Amber
                }
                else if (loan.Status == LoanStatus.Auctioned)
                {
                    statusText = "Auctioned (ஏலம்)";
                    bgBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)); // Slate
                    fgBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                }
                else
                {
                    statusText = "Active (நடப்பு)";
                    bgBrush = new SolidColorBrush(Color.FromRgb(239, 246, 255)); // Light Blue
                    fgBrush = new SolidColorBrush(Color.FromRgb(30, 64, 175));   // Dark Blue
                }

                var jewelDesc = loan.Items != null && loan.Items.Count > 0
                    ? string.Join(", ", loan.Items.Select(i => $"{i.JewelType?.Name ?? "Item"} ({i.Quantity} pcs, {i.NetWeight:F3}g)"))
                    : $"{loan.NetWeight:F3}g Gold";

                string closedOrLastPaid;
                if (loan.Status == LoanStatus.Closed)
                {
                    closedOrLastPaid = loan.ClosedDate.HasValue
                        ? $"Closed: {loan.ClosedDate.Value:dd-MMM-yy}"
                        : "Closed";
                }
                else if (loan.LastInterestPaidDate.HasValue)
                {
                    closedOrLastPaid = $"Paid: {loan.LastInterestPaidDate.Value:dd-MMM-yy}";
                }
                else
                {
                    closedOrLastPaid = "None";
                }

                viewModels.Add(new LoanDirectoryItemViewModel
                {
                    LoanId = loan.Id,
                    LoanNumber = loan.LoanNumber,
                    CustomerDisplayName = $"{loan.Customer.FullName} ({loan.Customer.Code})",
                    CustomerMobile = loan.Customer.MobileNumber,
                    PledgeDate = loan.PledgeDate,
                    MaturityDate = loan.MaturityDate,
                    SchemeInfo = $"{loan.Scheme?.Name} ({loan.Scheme?.InterestRatePerMonth:F2}%)",
                    JewelDetails = jewelDesc,
                    DisbursedAmount = loan.LoanAmount,
                    PrincipalPaid = principalPaid,
                    RemainingPrincipal = remainingPrincipal,
                    Status = loan.Status,
                    StatusText = statusText,
                    BadgeBackground = bgBrush,
                    BadgeForeground = fgBrush,
                    ClosedOrLastPaidText = closedOrLastPaid,
                    PacketNumber = loan.PacketNumber ?? "-",
                    IsClosed = loan.Status == LoanStatus.Closed,
                    IsOpen = loan.Status == LoanStatus.Active || loan.Status == LoanStatus.PartiallyPaid
                });
            }

            _allItems = viewModels;
            GridLoans.ItemsSource = _allItems;

            // 5. Update KPI Stat Cards
            var allLoansInDb = await context.Loans
                .AsNoTracking()
                .Include(l => l.Receipts)
                .Where(l => !l.IsDeleted)
                .ToListAsync();

            var totalLoansCount = allLoansInDb.Count;
            var totalDisbursedSum = allLoansInDb.Sum(l => l.LoanAmount);

            var activeList = allLoansInDb.Where(l => l.Status == LoanStatus.Active).ToList();
            var partPaidList = allLoansInDb.Where(l => l.Status == LoanStatus.PartiallyPaid).ToList();
            var closedList = allLoansInDb.Where(l => l.Status == LoanStatus.Closed).ToList();
            var overdueList = allLoansInDb.Where(l => (l.Status == LoanStatus.Active || l.Status == LoanStatus.PartiallyPaid) && l.MaturityDate < today).ToList();

            decimal activeBalSum = 0m;
            foreach (var al in activeList)
            {
                var paid = al.Receipts.Where(r => !r.IsDeleted).Sum(r => r.PrincipalAmount);
                activeBalSum += Math.Max(0m, al.LoanAmount - paid);
            }

            decimal partPaidBalSum = 0m;
            decimal partPrincipalPaidSum = 0m;
            foreach (var ppl in partPaidList)
            {
                var paid = ppl.Receipts.Where(r => !r.IsDeleted).Sum(r => r.PrincipalAmount);
                partPrincipalPaidSum += paid;
                partPaidBalSum += Math.Max(0m, ppl.LoanAmount - paid);
            }

            decimal closedDisbursedSum = closedList.Sum(l => l.LoanAmount);
            decimal overdueBalSum = 0m;
            foreach (var ol in overdueList)
            {
                var paid = ol.Receipts.Where(r => !r.IsDeleted).Sum(r => r.PrincipalAmount);
                overdueBalSum += Math.Max(0m, ol.LoanAmount - paid);
            }

            // Update UI Cards
            TxtKpiTotalCount.Text = $"{totalLoansCount} Loans";
            TxtKpiTotalAmount.Text = ValidationHelper.FormatCurrency(totalDisbursedSum);

            TxtKpiActiveCount.Text = $"{activeList.Count} Active";
            TxtKpiActiveBalance.Text = $"Bal: {ValidationHelper.FormatCurrency(activeBalSum)}";

            TxtKpiPartiallyPaidCount.Text = $"{partPaidList.Count} Part-Paid";
            TxtKpiPartPrincipalPaid.Text = $"Paid: {ValidationHelper.FormatCurrency(partPrincipalPaidSum)}";

            TxtKpiClosedCount.Text = $"{closedList.Count} Closed";
            TxtKpiClosedAmount.Text = $"{ValidationHelper.FormatCurrency(closedDisbursedSum)} Settled";

            TxtKpiOverdueCount.Text = $"{overdueList.Count} Overdue";
            TxtKpiOverdueAmount.Text = $"Due: {ValidationHelper.FormatCurrency(overdueBalSum)}";

            // Footer and Summary
            TxtTableSummary.Text = $"Showing {_allItems.Count} Loans (Filtered from {totalLoansCount} total)";
            TxtFooterAggregate.Text = $"Visible Loans: {_allItems.Count} | Disbursed: {ValidationHelper.FormatCurrency(_allItems.Sum(i => i.DisbursedAmount))} | Balance Remaining: {ValidationHelper.FormatCurrency(_allItems.Sum(i => i.RemainingPrincipal))} | Closed: {_allItems.Count(i => i.IsClosed)}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load loan directory data.");
            MessageBox.Show($"Failed to load loan data: {ex.Message}", "Query Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await LoadLoanDirectoryAsync();
        }
    }

    private async void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!_isInitialized) return;
        await LoadLoanDirectoryAsync();
    }

    private async void OnApplyFilterClick(object sender, RoutedEventArgs e)
    {
        await LoadLoanDirectoryAsync();
    }

    private async void OnResetFilterClick(object sender, RoutedEventArgs e)
    {
        TxtSearchQuery.Text = string.Empty;
        CmbStatusFilter.SelectedIndex = 0;
        if (CmbSchemeFilter.Items.Count > 0) CmbSchemeFilter.SelectedIndex = 0;
        DpFromDate.SelectedDate = null;
        DpToDate.SelectedDate = null;
        await LoadLoanDirectoryAsync();
    }

    private void OnSettleLoanRowClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LoanDirectoryItemViewModel item)
        {
            var win = Window.GetWindow(this) as MainWindow;
            if (win != null)
            {
                var receiptControl = new LoanReceiptControl(
                    _loanService,
                    _receiptService,
                    new StandardInterestCalculator(),
                    _currentUser);
                win.MainContentArea.Content = receiptControl;
                _ = receiptControl.LoadLoanAsync(item.LoanNumber);
            }
        }
    }

    private void OnViewHistoryRowClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LoanDirectoryItemViewModel item)
        {
            var win = Window.GetWindow(this) as MainWindow;
            if (win != null)
            {
                var historyControl = new LoanHistoryControl(_loanService);
                win.MainContentArea.Content = historyControl;
                _ = historyControl.LoadHistoryAsync(item.LoanNumber);
            }
        }
    }

    private void OnGridRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridLoans.SelectedItem is LoanDirectoryItemViewModel item)
        {
            OnViewHistoryRowClick(sender, e);
        }
    }

    private void OnPrintDirectoryClick(object sender, RoutedEventArgs e)
    {
        if (_allItems.Count == 0)
        {
            MessageBox.Show("No loans available to print.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(GridLoans, "JGF All Loans & Status Directory");
                MessageBox.Show("Loan directory successfully sent to printer.", "Printed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print loan directory.");
            MessageBox.Show($"Failed to print: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (_allItems.Count == 0)
        {
            MessageBox.Show("No loans to export.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var sfd = new SaveFileDialog
            {
                FileName = $"JGF_All_Loans_Directory_{DateTime.Today:yyyyMMdd}.csv",
                Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
                Title = "Save Loans Directory as CSV"
            };

            if (sfd.ShowDialog() == true)
            {
                using var writer = new StreamWriter(sfd.FileName);
                await writer.WriteLineAsync("JEBA GOLD FINANCE - ALL LOANS & STATUS DIRECTORY");
                await writer.WriteLineAsync($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("GL Number,Customer,Mobile,Pledge Date,Maturity Date,Scheme,Jewels,Loan Given (₹),Principal Paid (₹),Balance Remaining (₹),Status,Closed/Paid Date,Packet #");

                foreach (var item in _allItems)
                {
                    await writer.WriteLineAsync($"\"{item.LoanNumber}\",\"{item.CustomerDisplayName}\",\"{item.CustomerMobile}\",\"{item.PledgeDate:dd-MMM-yyyy}\",\"{item.MaturityDate:dd-MMM-yyyy}\",\"{item.SchemeInfo}\",\"{item.JewelDetails}\",{item.DisbursedAmount:F2},{item.PrincipalPaid:F2},{item.RemainingPrincipal:F2},\"{item.StatusText}\",\"{item.ClosedOrLastPaidText}\",\"{item.PacketNumber}\"");
                }

                await writer.WriteLineAsync();
                await writer.WriteLineAsync($"Total Count: {_allItems.Count}");
                await writer.WriteLineAsync($"Total Loan Given: ₹ {_allItems.Sum(i => i.DisbursedAmount):N2}");
                await writer.WriteLineAsync($"Total Principal Repaid: ₹ {_allItems.Sum(i => i.PrincipalPaid):N2}");
                await writer.WriteLineAsync($"Total Remaining Balance: ₹ {_allItems.Sum(i => i.RemainingPrincipal):N2}");
                await writer.WriteLineAsync($"Total Closed Loans: {_allItems.Count(i => i.IsClosed)}");

                MessageBox.Show($"Loan directory exported successfully to:\n{sfd.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export loans directory.");
            MessageBox.Show($"Failed to export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
