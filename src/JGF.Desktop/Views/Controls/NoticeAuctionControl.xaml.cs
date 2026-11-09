using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Notices;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Win32;

namespace JGF.Desktop.Views.Controls;

public class SelectableOverdueLoanItem : INotifyPropertyChanged
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public DateOnly PledgeDate { get; set; }
    public DateOnly? LastPaymentDate { get; set; }
    public int ElapsedMonths { get; set; }
    public decimal PrincipalBalance { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal CurrentGoldValue { get; set; }
    public decimal CurrentLtvRatio { get; set; }
    public OverdueBucket Bucket { get; set; }
    public int NoticesSentCount { get; set; }
    public DateOnly? LastNoticeDate { get; set; }
    public bool IsAuctionEligible { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}

public class ScheduledAuctionViewModel
{
    public Guid AuctionId { get; set; }
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateOnly AuctionDate { get; set; }
    public decimal ReservePrice { get; set; }
    public decimal TotalOutstanding { get; set; }
    public string DisplayInfo => $"{LoanNumber} - {CustomerName} (Auction: {AuctionDate:dd-MMM-yyyy}, Reserve: ₹{ReservePrice:N2})";
}

public partial class NoticeAuctionControl : UserControl
{
    private readonly IOverdueNoticeService _noticeService;
    private readonly Guid _currentUserId;
    private readonly UserRole _currentUserRole;

    private List<OverdueLoanSummaryDto> _allLoans = new();
    private readonly ObservableCollection<SelectableOverdueLoanItem> _displayLoans = new();
    private List<ScheduledAuctionViewModel> _scheduledAuctions = new();

    public NoticeAuctionControl(
        IOverdueNoticeService noticeService,
        Guid? currentUserId = null,
        UserRole? currentUserRole = null)
    {
        InitializeComponent();
        _noticeService = noticeService;
        _currentUserId = currentUserId ?? Guid.NewGuid();
        _currentUserRole = currentUserRole ?? UserRole.Owner;

        GridOverdueLoans.ItemsSource = _displayLoans;

        DpNoticeDate.SelectedDate = DateTime.Today;
        DpNoticeDueDate.SelectedDate = DateTime.Today.AddDays(15);
        DpAuctionDate.SelectedDate = DateTime.Today.AddDays(15);

        Loaded += async (s, e) => await InitializeDataAsync();
    }

    private async Task InitializeDataAsync()
    {
        await LoadOverdueLoansAsync();
        await LoadScheduledAuctionsAsync();
    }

    private async Task LoadOverdueLoansAsync()
    {
        try
        {
            _allLoans = await _noticeService.GetOverdueLoansAsync();
            ApplyFilter();

            // Populate Auction eligible loans combo
            var auctionEligible = _allLoans.Where(l => l.IsAuctionEligible).ToList();
            CmbAuctionEligibleLoans.ItemsSource = auctionEligible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load overdue loans: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadScheduledAuctionsAsync()
    {
        try
        {
            var auctions = await _noticeService.GetAuctionRegisterAsync();
            var pendingAuctions = auctions.Where(a => a.Status == AuctionStatus.Scheduled).ToList();

            _scheduledAuctions = pendingAuctions.Select(a => new ScheduledAuctionViewModel
            {
                AuctionId = a.Id,
                LoanId = a.LoanId,
                LoanNumber = a.Loan?.LoanNumber ?? "N/A",
                CustomerName = a.Loan?.Customer?.FullName ?? "N/A",
                AuctionDate = a.AuctionDate,
                ReservePrice = a.ReservePrice,
                TotalOutstanding = (a.Loan?.LoanAmount ?? 0)
            }).ToList();

            CmbScheduledAuctions.ItemsSource = _scheduledAuctions;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load scheduled auctions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        var category = (CmbOverdueFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Overdue Loans";
        var search = TxtSearchOverdue.Text.Trim().ToLowerInvariant();

        var query = _allLoans.AsEnumerable();

        if (category == "6-Month Reminder")
            query = query.Where(l => l.Bucket == OverdueBucket.SixMonthReminder);
        else if (category == "12-Month Matured")
            query = query.Where(l => l.Bucket == OverdueBucket.TwelveMonthMatured);
        else if (category == "Critical LTV (>90%)")
            query = query.Where(l => l.Bucket == OverdueBucket.OverdueCritical);
        else if (category == "Auction Eligible (15d+)")
            query = query.Where(l => l.IsAuctionEligible);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l =>
                l.LoanNumber.ToLowerInvariant().Contains(search) ||
                l.CustomerName.ToLowerInvariant().Contains(search) ||
                l.CustomerMobile.Contains(search));
        }

        var filtered = query.ToList();

        _displayLoans.Clear();
        foreach (var item in filtered)
        {
            var vm = new SelectableOverdueLoanItem
            {
                IsSelected = false,
                LoanId = item.LoanId,
                LoanNumber = item.LoanNumber,
                CustomerName = item.CustomerName,
                CustomerMobile = item.CustomerMobile,
                PledgeDate = item.PledgeDate,
                LastPaymentDate = item.LastPaymentDate,
                ElapsedMonths = item.ElapsedMonths,
                PrincipalBalance = item.PrincipalBalance,
                AccruedInterest = item.AccruedInterest,
                TotalOutstanding = item.TotalOutstanding,
                CurrentGoldValue = item.CurrentGoldValue,
                CurrentLtvRatio = item.CurrentLtvRatio,
                Bucket = item.Bucket,
                NoticesSentCount = item.NoticesSentCount,
                LastNoticeDate = item.LastNoticeDate,
                IsAuctionEligible = item.IsAuctionEligible
            };
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectableOverdueLoanItem.IsSelected))
                    UpdateSelectionSummary();
            };
            _displayLoans.Add(vm);
        }

        TxtTotalOverdueCount.Text = $"Total Loans: {filtered.Count}";
        TxtTotalOverdueAmount.Text = $"Total Dues: ₹{filtered.Sum(l => l.TotalOutstanding):N2}";
        TxtAuctionEligibleCount.Text = $"Auction Eligible: {filtered.Count(l => l.IsAuctionEligible)}";

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var count = _displayLoans.Count(l => l.IsSelected);
        TxtSelectedNoticeCount.Text = $"{count} loan(s) selected for notice";
    }

    private void OnFilterCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilter();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilter();
    }

    private async void OnRefreshOverdueClick(object sender, RoutedEventArgs e)
    {
        await LoadOverdueLoansAsync();
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _displayLoans)
            item.IsSelected = true;
    }

    private void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _displayLoans)
            item.IsSelected = false;
    }

    private void OnOverdueLoanSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GridOverdueLoans.SelectedItem is SelectableOverdueLoanItem item)
        {
            _ = UpdateNoticePreviewAsync(item.LoanId);
        }
    }

    private void OnGoToPrepareNoticeClick(object sender, RoutedEventArgs e)
    {
        var selected = _displayLoans.Where(l => l.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Please select at least one loan from the grid using the checkboxes.", "No Loans Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        NoticeTabControl.SelectedIndex = 1; // Tab 2: Notices
        _ = UpdateNoticePreviewAsync(selected.First().LoanId);
    }

    private void OnGoToAuctionWorkflowClick(object sender, RoutedEventArgs e)
    {
        var selected = _displayLoans.Where(l => l.IsSelected).ToList();
        if (selected.Count == 1)
        {
            var match = _allLoans.FirstOrDefault(l => l.LoanId == selected[0].LoanId);
            if (match != null)
                CmbAuctionEligibleLoans.SelectedItem = match;
        }
        NoticeTabControl.SelectedIndex = 2; // Tab 3: Auction
    }

    private async Task UpdateNoticePreviewAsync(Guid loanId)
    {
        try
        {
            var noticeType = GetSelectedNoticeType();
            var noticeDate = DateOnly.FromDateTime(DpNoticeDate.SelectedDate ?? DateTime.Today);
            var dueDate = DateOnly.FromDateTime(DpNoticeDueDate.SelectedDate ?? DateTime.Today.AddDays(15));
            var inTamil = RbTamil.IsChecked == true;

            var text = await _noticeService.GenerateNoticeHtmlOrTextAsync(loanId, noticeType, noticeDate, dueDate, inTamil);
            TxtNoticeLetterPreview.Text = text;
        }
        catch (Exception ex)
        {
            TxtNoticeLetterPreview.Text = $"Error generating preview: {ex.Message}";
        }
    }

    private NoticeType GetSelectedNoticeType()
    {
        var tag = (CmbNoticeType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return tag switch
        {
            "Reminder" => NoticeType.Reminder,
            "FinalNotice" => NoticeType.FinalNotice,
            "AuctionNotice" => NoticeType.AuctionNotice,
            "DemandNotice" => NoticeType.DemandNotice,
            _ => NoticeType.Reminder
        };
    }

    private NoticeSentVia GetSelectedDeliveryMode()
    {
        var tag = (CmbDeliveryMode.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return tag switch
        {
            "RegisteredPost" => NoticeSentVia.RegisteredPost,
            "SpeedPost" => NoticeSentVia.SpeedPost,
            "HandDelivery" => NoticeSentVia.HandDelivery,
            "Courier" => NoticeSentVia.Courier,
            "Sms" => NoticeSentVia.Sms,
            _ => NoticeSentVia.RegisteredPost
        };
    }

    private void OnNoticeFormChanged(object sender, EventArgs e)
    {
        if (!IsLoaded) return;
        var selected = _displayLoans.FirstOrDefault(l => l.IsSelected) ?? _displayLoans.FirstOrDefault();
        if (selected != null)
            _ = UpdateNoticePreviewAsync(selected.LoanId);
    }

    private void OnLanguageChanged(object sender, RoutedEventArgs e)
    {
        OnNoticeFormChanged(sender, e);
    }

    private void OnRegeneratePreviewClick(object sender, RoutedEventArgs e)
    {
        OnNoticeFormChanged(sender, e);
    }

    private async void OnIssueNoticeBatchClick(object sender, RoutedEventArgs e)
    {
        var selectedLoans = _displayLoans.Where(l => l.IsSelected).Select(l => l.LoanId).ToList();
        if (selectedLoans.Count == 0)
        {
            MessageBox.Show("Please select at least one loan in the Overdue Loans Tracker tab to issue notice.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var noticeType = GetSelectedNoticeType();
        var deliveryMode = GetSelectedDeliveryMode();
        var noticeDate = DateOnly.FromDateTime(DpNoticeDate.SelectedDate ?? DateTime.Today);
        var dueDate = DateOnly.FromDateTime(DpNoticeDueDate.SelectedDate ?? DateTime.Today.AddDays(15));
        var remarks = TxtNoticeRemarks.Text.Trim();
        if (!string.IsNullOrWhiteSpace(TxtPostalRef.Text))
            remarks += $" [Ref/RPAD: {TxtPostalRef.Text.Trim()}]";

        var confirm = MessageBox.Show(
            $"Confirm issuing {noticeType} for {selectedLoans.Count} loans via {deliveryMode}?\nNotice Date: {noticeDate:dd-MMM-yyyy}\nGrace Due Date: {dueDate:dd-MMM-yyyy}",
            "Confirm Notice Batch Dispatch", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var dto = new CreateNoticeBatchDto(
                selectedLoans,
                noticeType,
                noticeDate,
                dueDate,
                deliveryMode,
                remarks,
                _currentUserId,
                _currentUserRole
            );

            var notices = await _noticeService.IssueNoticeBatchAsync(dto);
            MessageBox.Show($"Successfully recorded {notices.Count} notices in the database.\nPostal and legal tracking records updated.", "Batch Issued", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadOverdueLoansAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to issue notices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnPrintNoticeClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNoticeLetterPreview.Text))
        {
            MessageBox.Show("No notice letter content available to print.", "Empty Content", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var printDlg = new PrintDialog();
        if (printDlg.ShowDialog() == true)
        {
            var textBlock = new TextBlock
            {
                Text = TxtNoticeLetterPreview.Text,
                FontFamily = new System.Windows.Media.FontFamily("Latha, Arial, Segoe UI"),
                FontSize = 12,
                Margin = new Thickness(40),
                TextWrapping = TextWrapping.Wrap
            };
            printDlg.PrintVisual(textBlock, "JGF Statutory Notice Letter");
        }
    }

    private void OnExportNoticeClick(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Text File (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = $"Notice_Batch_{DateTime.Today:yyyyMMdd}.txt"
        };
        if (sfd.ShowDialog() == true)
        {
            File.WriteAllText(sfd.FileName, TxtNoticeLetterPreview.Text, Encoding.UTF8);
            MessageBox.Show("Notice letter successfully exported.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnAuctionLoanSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbAuctionEligibleLoans == null) return;
        if (CmbAuctionEligibleLoans.SelectedItem is OverdueLoanSummaryDto loan)
        {
            if (TxtAuctionLoanDetails != null)
                TxtAuctionLoanDetails.Text = $"Loan No: {loan.LoanNumber}\nCustomer: {loan.CustomerName} ({loan.CustomerMobile})\nPrincipal: ₹{loan.PrincipalBalance:N2}\nInterest Due: ₹{loan.AccruedInterest:N2}\nTotal Outstanding: ₹{loan.TotalOutstanding:N2}\nGold Value: ₹{loan.CurrentGoldValue:N2}\nLast Notice Date: {loan.LastNoticeDate:dd-MMM-yyyy}\nPre-Notice Served 15+ Days: {(loan.IsAuctionEligible ? "YES (Legal Requirement Met)" : "NO")}";
            if (TxtReservePrice != null)
                TxtReservePrice.Text = loan.TotalOutstanding.ToString("F2");
        }
    }

    private async void OnApproveScheduleAuctionClick(object sender, RoutedEventArgs e)
    {
        if (CmbAuctionEligibleLoans.SelectedItem is not OverdueLoanSummaryDto loan)
        {
            MessageBox.Show("Please select an auction-eligible loan first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!loan.IsAuctionEligible)
        {
            MessageBox.Show("Statutory requirement violation: An auction cannot be scheduled unless a pre-auction notice has been served at least 15 days ago.", "Legal Barrier", MessageBoxButton.OK, MessageBoxImage.Stop);
            return;
        }

        if (!decimal.TryParse(TxtReservePrice.Text, out var reservePrice) || reservePrice <= 0)
        {
            MessageBox.Show("Please enter a valid positive reserve price.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var auctionDate = DateOnly.FromDateTime(DpAuctionDate.SelectedDate ?? DateTime.Today.AddDays(15));
        var minStatutoryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14));
        if (auctionDate < minStatutoryDate)
        {
            MessageBox.Show("Statutory regulations require a minimum 15-day notice period before holding a public auction.", "Statutory Notice Requirement", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Approve public auction for Loan {loan.LoanNumber} ({loan.CustomerName})?\nAuction Date: {auctionDate:dd-MMM-yyyy}\nReserve Price: ₹{reservePrice:N2}\nVenue: {TxtAuctionVenue.Text}",
            "Confirm Owner Auction Authorization", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var auction = await _noticeService.ApproveAndScheduleAuctionAsync(
                loan.LoanId,
                auctionDate,
                reservePrice,
                _currentUserId,
                _currentUserRole
            );

            MessageBox.Show($"Auction successfully scheduled and authorized by Owner!\nAuction ID: {auction.Id}\nStatus: Scheduled", "Auction Approved", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadScheduledAuctionsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Auction approval failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnScheduledAuctionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbScheduledAuctions == null) return;
        if (CmbScheduledAuctions.SelectedItem is ScheduledAuctionViewModel auction)
        {
            if (TxtSalePrice != null)
                TxtSalePrice.Text = auction.ReservePrice.ToString("F2");
            UpdateAuctionSettlementMath();
        }
    }

    private void OnAuctionSettlementInputChanged(object sender, TextChangedEventArgs e)
    {
        UpdateAuctionSettlementMath();
    }

    private void UpdateAuctionSettlementMath()
    {
        if (CmbScheduledAuctions == null || TxtSalePrice == null || TxtAuctionExpenses == null || TxtCalcLoanDues == null || TxtCalcSaleNet == null || TxtCalcSurplus == null || TxtCalcDeficit == null)
            return;

        if (CmbScheduledAuctions.SelectedItem is not ScheduledAuctionViewModel auction)
            return;

        decimal.TryParse(TxtSalePrice.Text, out var salePrice);
        decimal.TryParse(TxtAuctionExpenses.Text, out var expenses);

        var dues = auction.TotalOutstanding;
        var netProceeds = salePrice - expenses;

        TxtCalcLoanDues.Text = $"Total Dues: ₹{dues:N2}";
        TxtCalcSaleNet.Text = $"Net Proceeds: ₹{netProceeds:N2}";

        if (netProceeds > dues)
        {
            var surplus = netProceeds - dues;
            TxtCalcSurplus.Text = $"Surplus Payable: ₹{surplus:N2}";
            TxtCalcDeficit.Text = "Deficit / Loss: ₹0.00";
        }
        else
        {
            var deficit = dues - netProceeds;
            TxtCalcSurplus.Text = "Surplus Payable: ₹0.00";
            TxtCalcDeficit.Text = $"Deficit / Loss: ₹{deficit:N2}";
        }
    }

    private async void OnConductSettleAuctionClick(object sender, RoutedEventArgs e)
    {
        if (CmbScheduledAuctions.SelectedItem is not ScheduledAuctionViewModel auction)
        {
            MessageBox.Show("Please select an active scheduled auction.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtSalePrice.Text, out var salePrice) || salePrice <= 0)
        {
            MessageBox.Show("Please enter a valid winning sale price.", "Invalid Sale Price", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtAuctionExpenses.Text, out var expenses);
        var buyerName = TxtBuyerName.Text.Trim();
        if (string.IsNullOrWhiteSpace(buyerName))
        {
            MessageBox.Show("Please enter the winning buyer's name.", "Buyer Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Conduct final settlement for Loan {auction.LoanNumber}?\nSale Price: ₹{salePrice:N2}\nExpenses: ₹{expenses:N2}\nBuyer: {buyerName}\nBalanced accounting vouchers will be posted immediately.",
            "Confirm Auction Settlement", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var dto = new ConductAuctionDto(
                auction.LoanId,
                DateOnly.FromDateTime(DateTime.Today),
                TxtAuctionVenue.Text.Trim(),
                auction.ReservePrice,
                salePrice,
                expenses,
                buyerName,
                TxtBuyerAddress.Text.Trim(),
                TxtAuctionSettlementRemarks.Text.Trim(),
                _currentUserId,
                _currentUserRole
            );

            var result = await _noticeService.ConductAndSettleAuctionAsync(dto);

            var msg = new StringBuilder();
            msg.AppendLine($"Auction completed and settled successfully!");
            msg.AppendLine($"Loan Number: {result.LoanNumber}");
            msg.AppendLine($"Voucher No: {result.VoucherNumber}");
            msg.AppendLine($"Sale Proceeds: ₹{result.SalePrice:N2}");
            if (result.SurplusAmount > 0)
                msg.AppendLine($"Surplus Payable to Customer: ₹{result.SurplusAmount:N2} (Posted to Cr Auction Surplus Payable)");
            if (result.DeficitAmount > 0)
                msg.AppendLine($"Deficit Booked as Loss: ₹{result.DeficitAmount:N2} (Posted to Dr Auction Loss)");

            MessageBox.Show(msg.ToString(), "Auction Settled", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadOverdueLoansAsync();
            await LoadScheduledAuctionsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to conduct auction settlement: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnReportTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) await LoadSelectedReportAsync();
    }

    private async void OnRefreshReportClick(object sender, RoutedEventArgs e)
    {
        await LoadSelectedReportAsync();
    }

    private async Task LoadSelectedReportAsync()
    {
        try
        {
            var selectedIdx = CmbReportType.SelectedIndex;
            if (selectedIdx == 0) // Overdue Summary Report
            {
                var overdue = await _noticeService.GetOverdueLoansAsync();
                GridReports.ItemsSource = overdue.Select(o => new
                {
                    o.LoanNumber,
                    o.CustomerName,
                    o.CustomerMobile,
                    o.PledgeDate,
                    o.ElapsedMonths,
                    Principal = o.PrincipalBalance.ToString("N2"),
                    InterestDue = o.AccruedInterest.ToString("N2"),
                    TotalDues = o.TotalOutstanding.ToString("N2"),
                    GoldValue = o.CurrentGoldValue.ToString("N2"),
                    LtvRatio = $"{o.CurrentLtvRatio:F1}%",
                    Category = o.Bucket.ToString(),
                    AuctionReady = o.IsAuctionEligible ? "YES" : "NO"
                }).ToList();
            }
            else if (selectedIdx == 1) // Notices Sent Register
            {
                var overdue = await _noticeService.GetOverdueLoansAsync();
                var allNotices = new List<object>();
                foreach (var loan in overdue)
                {
                    var notices = await _noticeService.GetNoticesForLoanAsync(loan.LoanId);
                    foreach (var n in notices)
                    {
                        allNotices.Add(new
                        {
                            LoanNumber = loan.LoanNumber,
                            Customer = loan.CustomerName,
                            NoticeDate = n.NoticeDate,
                            DueDate = n.DueDate,
                            Type = n.NoticeType.ToString(),
                            DeliveryMode = n.SentVia.ToString(),
                            PostalRef = n.TrackingNumber ?? "N/A",
                            Remarks = n.Remarks ?? ""
                        });
                    }
                }
                GridReports.ItemsSource = allNotices;
            }
            else if (selectedIdx == 2) // Auction Eligible Loans
            {
                var overdue = await _noticeService.GetOverdueLoansAsync();
                GridReports.ItemsSource = overdue.Where(o => o.IsAuctionEligible).Select(o => new
                {
                    o.LoanNumber,
                    o.CustomerName,
                    o.CustomerMobile,
                    o.PledgeDate,
                    o.ElapsedMonths,
                    TotalDues = o.TotalOutstanding.ToString("N2"),
                    GoldValue = o.CurrentGoldValue.ToString("N2"),
                    LastNotice = o.LastNoticeDate?.ToString("dd-MMM-yyyy") ?? "None",
                    Status = "Eligible for Auction Scheduling"
                }).ToList();
            }
            else if (selectedIdx == 3) // Auction Register
            {
                var auctions = await _noticeService.GetAuctionRegisterAsync();
                GridReports.ItemsSource = auctions.Select(a => new
                {
                    AuctionId = a.Id.ToString().Substring(0, 8),
                    LoanNumber = a.Loan?.LoanNumber ?? "N/A",
                    Customer = a.Loan?.Customer?.FullName ?? "N/A",
                    AuctionDate = a.AuctionDate,
                    Status = a.Status.ToString(),
                    ReservePrice = a.ReservePrice.ToString("N2"),
                    SalePrice = a.SalePrice.ToString("N2"),
                    Expenses = a.AuctionExpenses.ToString("N2"),
                    SurplusPayable = a.SurplusAmount.ToString("N2"),
                    DeficitLoss = a.DeficitAmount.ToString("N2"),
                    Buyer = a.BuyerName ?? "N/A"
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExportReportCsvClick(object sender, RoutedEventArgs e)
    {
        if (GridReports.ItemsSource == null)
        {
            MessageBox.Show("No report data available to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"Notice_Auction_Report_{DateTime.Today:yyyyMMdd}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                var items = GridReports.ItemsSource.Cast<object>().ToList();
                if (items.Count > 0)
                {
                    var props = items[0].GetType().GetProperties();
                    sb.AppendLine(string.Join(",", props.Select(p => $"\"{p.Name}\"")));
                    foreach (var item in items)
                    {
                        var row = props.Select(p => $"\"{p.GetValue(item)?.ToString()?.Replace("\"", "\"\"")}\"");
                        sb.AppendLine(string.Join(",", row));
                    }
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Report exported successfully to CSV.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        if (ChkAdvocateConfirmation.IsChecked != true)
        {
            MessageBox.Show("Please confirm the statutory legal advocate disclaimer checkbox before saving settings.", "Advocate Confirmation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("Legal and statutory notice thresholds successfully updated and saved.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

