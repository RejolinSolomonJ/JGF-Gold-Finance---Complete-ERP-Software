using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Loans;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Win32;

namespace JGF.Desktop.Views.Controls;

public class LoanItemRowViewModel
{
    public Guid JewelTypeId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal GrossWeight { get; set; }
    public decimal DeductionWeight { get; set; }
    public decimal NetWeight => Math.Max(0m, GrossWeight - DeductionWeight);
    public decimal PurityPercent { get; set; } = 91.60m;
    public decimal RatePerGram { get; set; } = 5500.00m;
    public decimal AppraisedValue => Math.Round(NetWeight * RatePerGram * (PurityPercent / 100m), 2);
}

public partial class NewLoanEntryControl : UserControl
{
    private readonly ILoanService _loanService;
    private readonly ISchemeService _schemeService;
    private readonly ICustomerService _customerService;
    private readonly IGoldRateService _goldRateService;
    private readonly IJewelTypeService _jewelTypeService;

    private Customer? _selectedCustomer;
    private Scheme? _selectedScheme;
    private decimal _today22KRate = 5500.00m;

    public ObservableCollection<LoanItemRowViewModel> Items { get; } = new();

    public NewLoanEntryControl(
        ILoanService loanService,
        ISchemeService schemeService,
        ICustomerService customerService,
        IGoldRateService goldRateService,
        IJewelTypeService jewelTypeService)
    {
        InitializeComponent();
        _loanService = loanService;
        _schemeService = schemeService;
        _customerService = customerService;
        _goldRateService = goldRateService;
        _jewelTypeService = jewelTypeService;

        GridItems.ItemsSource = Items;
        DpPledgeDate.SelectedDate = DateTime.Today;

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Load Today's Gold Rate
        var rate = await _goldRateService.GetTodayRateAsync();
        if (rate != null) _today22KRate = rate.Rate22K;

        // Load Schemes
        var schemes = await _schemeService.GetAllSchemesAsync(activeOnly: true);
        CmbScheme.ItemsSource = schemes;
        CmbScheme.DisplayMemberPath = "Name";
        if (schemes.Count > 0) CmbScheme.SelectedIndex = 0;

        // Default item line
        if (Items.Count == 0)
        {
            var jewelTypes = await _jewelTypeService.GetAllJewelTypesAsync();
            var defType = jewelTypes.FirstOrDefault();
            Items.Add(new LoanItemRowViewModel
            {
                JewelTypeId = defType?.Id ?? Guid.NewGuid(),
                Description = "22K Gold Chain",
                Quantity = 1,
                GrossWeight = 20.000m,
                DeductionWeight = 1.500m,
                PurityPercent = 91.60m,
                RatePerGram = _today22KRate
            });
            RecalculateTotals();
        }
    }

    private void OnSchemeChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedScheme = CmbScheme.SelectedItem as Scheme;
        RecalculateTotals();
    }

    private void OnAddJewelItemClick(object sender, RoutedEventArgs e)
    {
        Items.Add(new LoanItemRowViewModel
        {
            Description = "Gold Ring",
            Quantity = 1,
            GrossWeight = 8.000m,
            DeductionWeight = 0.500m,
            PurityPercent = 91.60m,
            RatePerGram = _today22KRate
        });
        RecalculateTotals();
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.InvokeAsync(RecalculateTotals);
    }

    private void RecalculateTotals()
    {
        var totalNet = Items.Sum(i => i.NetWeight);
        var totalValue = Items.Sum(i => i.AppraisedValue);

        TxtTotalNet.Text = $"{totalNet:F3} g";
        TxtTotalValue.Text = $"₹ {totalValue:N2}";

        var ltvPercent = _selectedScheme?.MaxLtvPercentage ?? 75.00m;
        var maxEligible = Math.Round(totalValue * (ltvPercent / 100m), 2);
        TxtMaxEligible.Text = $"₹ {maxEligible:N2} ({ltvPercent}%)";

        decimal.TryParse(TxtLoanAmount.Text, out var loanAmt);
        if (loanAmt <= 0)
        {
            TxtLoanAmount.Text = maxEligible.ToString("F2");
            loanAmt = maxEligible;
        }

        var rate = _selectedScheme?.InterestRatePerMonth ?? 1.50m;
        var estInterest = Math.Round(loanAmt * (rate / 100m), 2);
        TxtEstMonthlyInterest.Text = $"₹ {estInterest:N2} / mo";

        if (loanAmt > maxEligible)
        {
            TxtOverrideNotice.Visibility = Visibility.Visible;
        }
        else
        {
            TxtOverrideNotice.Visibility = Visibility.Collapsed;
        }
    }

    private void OnLoanAmountChanged(object sender, TextChangedEventArgs e)
    {
        RecalculateTotals();
    }

    private async void OnPickCustomerClick(object sender, RoutedEventArgs e)
    {
        var customers = await _customerService.SearchCustomersAsync("", 1);
        if (customers.Count > 0)
        {
            _selectedCustomer = customers[0];
            TxtCustomerSearch.Text = $"{_selectedCustomer.Code} - {_selectedCustomer.FullName}";
        }
        else
        {
            MessageBox.Show("Please create a customer from the Member Detail screen first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnAttachJewelPhotoClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Attach Scale / Jewel Photo",
            Filter = "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
        };
        if (ofd.ShowDialog() == true)
        {
            TxtPhotoPath.Text = ofd.FileName;
        }
    }

    private async void OnIssueLoanClick(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null)
        {
            var customers = await _customerService.SearchCustomersAsync("", 1);
            if (customers.Count > 0) _selectedCustomer = customers[0];
            else
            {
                MessageBox.Show("Select or create a customer first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (_selectedScheme == null)
        {
            MessageBox.Show("Select a loan scheme.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtLoanAmount.Text, out var loanAmount);
        if (loanAmount <= 0)
        {
            MessageBox.Show("Enter a valid loan amount.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var itemDtos = Items.Select(i => new CreateLoanItemDto(
            i.JewelTypeId,
            i.Description,
            i.Quantity,
            i.GrossWeight,
            i.DeductionWeight,
            i.NetWeight,
            i.PurityPercent,
            i.RatePerGram,
            i.AppraisedValue
        )).ToList();

        var dto = new CreateLoanDto(
            _selectedCustomer.Id,
            _selectedScheme.Id,
            DateOnly.FromDateTime(DpPledgeDate.SelectedDate ?? DateTime.Today),
            loanAmount,
            itemDtos,
            TxtNominee.Text.Trim(),
            TxtNomineeRel.Text.Trim(),
            TxtPacket.Text.Trim(),
            TxtPhotoPath.Text.Trim(),
            TxtOverrideReason.Text.Trim(),
            Guid.Empty,
            UserRole.Owner
        );

        try
        {
            var loan = await _loanService.IssueNewLoanAsync(dto);
            MessageBox.Show($"Gold Loan #{loan.LoanNumber} issued successfully!\nVoucher posted: Dr Jewel Loan ₹{loan.LoanAmount:N2}, Cr Cash ₹{loan.LoanAmount:N2}", 
                "Loan Disbursed", MessageBoxButton.OK, MessageBoxImage.Information);
            OnClearClick(sender, e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Loan issuance failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Items.Clear();
        TxtOverrideReason.Text = "";
        TxtPhotoPath.Text = "";
        TxtNominee.Text = "";
        RecalculateTotals();
    }
}

