using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using JGF.Application.Loans;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Win32;
using Serilog;

namespace JGF.Desktop.Views.Controls;

public class LoanItemRowViewModel : ObservableObject
{
    private Guid _jewelTypeId;
    private string _description = string.Empty;
    private int _quantity = 1;
    private decimal _grossWeight;
    private decimal _deductionWeight;
    private decimal _purityPercent = 91.60m;
    private decimal _ratePerGram = 5500.00m;

    public Guid JewelTypeId
    {
        get => _jewelTypeId;
        set => SetProperty(ref _jewelTypeId, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public int Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, Math.Max(1, value));
    }

    public decimal GrossWeight
    {
        get => _grossWeight;
        set
        {
            if (SetProperty(ref _grossWeight, Math.Max(0m, value)))
            {
                OnPropertyChanged(nameof(NetWeight));
                OnPropertyChanged(nameof(AppraisedValue));
            }
        }
    }

    public decimal DeductionWeight
    {
        get => _deductionWeight;
        set
        {
            if (SetProperty(ref _deductionWeight, Math.Max(0m, value)))
            {
                OnPropertyChanged(nameof(NetWeight));
                OnPropertyChanged(nameof(AppraisedValue));
            }
        }
    }

    public decimal NetWeight => Math.Max(0m, GrossWeight - DeductionWeight);

    public decimal PurityPercent
    {
        get => _purityPercent;
        set
        {
            if (SetProperty(ref _purityPercent, Math.Clamp(value, 0m, 100m)))
            {
                OnPropertyChanged(nameof(AppraisedValue));
            }
        }
    }

    public decimal RatePerGram
    {
        get => _ratePerGram;
        set
        {
            if (SetProperty(ref _ratePerGram, Math.Max(0m, value)))
            {
                OnPropertyChanged(nameof(AppraisedValue));
            }
        }
    }

    public decimal AppraisedValue => Math.Round(NetWeight * RatePerGram * (PurityPercent / 100m), 2);
}

public partial class NewLoanEntryControl : UserControl
{
    private readonly ILoanService _loanService;
    private readonly ISchemeService _schemeService;
    private readonly ICustomerService _customerService;
    private readonly IGoldRateService _goldRateService;
    private readonly IJewelTypeService _jewelTypeService;
    private readonly User? _currentUser;

    private Customer? _selectedCustomer;
    private Scheme? _selectedScheme;
    private decimal _today22KRate = 5500.00m;
    private bool _isProgrammaticAmountChange = false;
    private Guid _defaultJewelTypeId = Guid.Empty;

    public ObservableCollection<LoanItemRowViewModel> Items { get; } = new();

    public NewLoanEntryControl(
        ILoanService loanService,
        ISchemeService schemeService,
        ICustomerService customerService,
        IGoldRateService goldRateService,
        IJewelTypeService jewelTypeService,
        User? currentUser = null)
    {
        InitializeComponent();
        _loanService = loanService;
        _schemeService = schemeService;
        _customerService = customerService;
        _goldRateService = goldRateService;
        _jewelTypeService = jewelTypeService;
        _currentUser = currentUser;

        GridItems.ItemsSource = Items;
        Items.CollectionChanged += OnItemsCollectionChanged;

        DpPledgeDate.SelectedDate = DateTime.Today;
        Loaded += async (s, e) => await InitializeAsync();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (LoanItemRowViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (LoanItemRowViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }
        RecalculateTotals();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoanItemRowViewModel.NetWeight) ||
            e.PropertyName == nameof(LoanItemRowViewModel.AppraisedValue) ||
            e.PropertyName == nameof(LoanItemRowViewModel.GrossWeight) ||
            e.PropertyName == nameof(LoanItemRowViewModel.DeductionWeight))
        {
            RecalculateTotals();
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Load Today's Gold Rate
            var rate = await _goldRateService.GetTodayRateAsync();
            if (rate != null) _today22KRate = rate.Rate22K;

            // Load Schemes
            var schemes = await _schemeService.GetAllSchemesAsync(activeOnly: true);
            CmbScheme.ItemsSource = schemes;
            CmbScheme.DisplayMemberPath = "Name";
            if (schemes.Count > 0) CmbScheme.SelectedIndex = 0;

            var jewelTypes = await _jewelTypeService.GetAllJewelTypesAsync();
            var defType = jewelTypes.FirstOrDefault();
            if (defType != null)
            {
                _defaultJewelTypeId = defType.Id;
            }

            // Default item line if empty
            if (Items.Count == 0)
            {
                Items.Add(new LoanItemRowViewModel
                {
                    JewelTypeId = _defaultJewelTypeId,
                    Description = "22K Gold Chain",
                    Quantity = 1,
                    GrossWeight = 20.000m,
                    DeductionWeight = 1.500m,
                    PurityPercent = 91.60m,
                    RatePerGram = _today22KRate
                });
            }

            RecalculateTotals();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize New Loan Entry screen.");
            MessageBox.Show($"Initialization error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            JewelTypeId = _defaultJewelTypeId,
            Description = "Gold Ornament",
            Quantity = 1,
            GrossWeight = 8.000m,
            DeductionWeight = 0.500m,
            PurityPercent = 91.60m,
            RatePerGram = _today22KRate
        });
    }

    private void RecalculateTotals()
    {
        if (Items == null || TxtTotalNet == null || TxtTotalValue == null || TxtMaxEligible == null || TxtLoanAmount == null || TxtEstMonthlyInterest == null || TxtOverrideNotice == null)
            return;

        var totalNet = Items.Sum(i => i.NetWeight);
        var totalValue = Items.Sum(i => i.AppraisedValue);

        TxtTotalNet.Text = ValidationHelper.FormatWeight(totalNet);
        TxtTotalValue.Text = ValidationHelper.FormatCurrency(totalValue);

        var ltvPercent = _selectedScheme?.MaxLtvPercentage ?? 75.00m;
        var maxEligible = Math.Round(totalValue * (ltvPercent / 100m), 2);
        TxtMaxEligible.Text = $"{ValidationHelper.FormatCurrency(maxEligible)} ({ltvPercent:F0}%)";

        decimal.TryParse(TxtLoanAmount.Text, out var loanAmt);

        // Only set default loan amount on initial load when loan amount is zero
        if (loanAmt <= 0 && maxEligible > 0 && !_isProgrammaticAmountChange && TxtLoanAmount.Text == "0.00")
        {
            _isProgrammaticAmountChange = true;
            TxtLoanAmount.Text = maxEligible.ToString("F2");
            loanAmt = maxEligible;
            _isProgrammaticAmountChange = false;
        }

        var rate = _selectedScheme?.InterestRatePerMonth ?? 1.50m;
        var estInterest = Math.Round(Math.Max(0m, loanAmt) * (rate / 100m), 2);
        TxtEstMonthlyInterest.Text = $"{ValidationHelper.FormatCurrency(estInterest)} / mo";

        if (loanAmt > maxEligible && maxEligible > 0)
        {
            TxtOverrideNotice.Visibility = Visibility.Visible;
        }
        else
        {
            TxtOverrideNotice.Visibility = Visibility.Collapsed;
        }

        ValidateInputs();
    }

    private void OnLoanAmountChanged(object sender, TextChangedEventArgs e)
    {
        if (_isProgrammaticAmountChange) return;
        RecalculateTotals();
    }

    private void OnPickCustomerClick(object sender, RoutedEventArgs e)
    {
        var parentWin = Window.GetWindow(this);
        var dlg = new CustomerLookupDialog(_customerService) { Owner = parentWin };
        if (dlg.ShowDialog() == true && dlg.SelectedCustomer != null)
        {
            _selectedCustomer = dlg.SelectedCustomer;
            TxtCustomerSearch.Text = $"{_selectedCustomer.Code} — {_selectedCustomer.FullName} ({_selectedCustomer.MobileNumber})";
            TxtCustomerError.Visibility = Visibility.Collapsed;
            ValidateInputs();
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

    private bool ValidateInputs()
    {
        bool isValid = true;

        if (_selectedCustomer == null)
        {
            isValid = false;
        }

        decimal.TryParse(TxtLoanAmount.Text, out var loanAmount);
        if (loanAmount <= 0)
        {
            TxtLoanAmountError.Visibility = Visibility.Visible;
            isValid = false;
        }
        else
        {
            TxtLoanAmountError.Visibility = Visibility.Collapsed;
        }

        var totalNet = Items.Sum(i => i.NetWeight);
        if (totalNet <= 0)
        {
            isValid = false;
        }

        var totalValue = Items.Sum(i => i.AppraisedValue);
        var ltvPercent = _selectedScheme?.MaxLtvPercentage ?? 75.00m;
        var maxEligible = Math.Round(totalValue * (ltvPercent / 100m), 2);

        if (loanAmount > maxEligible)
        {
            if (string.IsNullOrWhiteSpace(TxtOverrideReason.Text))
            {
                TxtOverrideError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                TxtOverrideError.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            TxtOverrideError.Visibility = Visibility.Collapsed;
        }

        BtnIssueLoan.IsEnabled = isValid;
        return isValid;
    }

    private async void OnIssueLoanClick(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null)
        {
            TxtCustomerError.Visibility = Visibility.Visible;
            MessageBox.Show("Please select a customer first.", "Customer Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedScheme == null)
        {
            MessageBox.Show("Please select a loan scheme.", "Scheme Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtLoanAmount.Text, out var loanAmount) || loanAmount <= 0)
        {
            MessageBox.Show("Please enter a valid loan amount greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var totalNet = Items.Sum(i => i.NetWeight);
        if (totalNet <= 0)
        {
            MessageBox.Show("Total net gold weight must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var totalValue = Items.Sum(i => i.AppraisedValue);
        var maxEligible = Math.Round(totalValue * ((_selectedScheme.MaxLtvPercentage) / 100m), 2);

        if (loanAmount > maxEligible && string.IsNullOrWhiteSpace(TxtOverrideReason.Text))
        {
            MessageBox.Show($"Loan amount {ValidationHelper.FormatCurrency(loanAmount)} exceeds scheme limit {ValidationHelper.FormatCurrency(maxEligible)}!\nAn Owner override reason is required.",
                "Override Reason Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtOverrideReason.Focus();
            return;
        }

        var itemDtos = Items.Select(i => new CreateLoanItemDto(
            i.JewelTypeId != Guid.Empty ? i.JewelTypeId : _defaultJewelTypeId,
            i.Description,
            i.Quantity,
            i.GrossWeight,
            i.DeductionWeight,
            i.NetWeight,
            i.PurityPercent,
            i.RatePerGram,
            i.AppraisedValue
        )).ToList();

        var userId = _currentUser?.Id ?? Guid.Empty;
        var userRole = _currentUser?.Role ?? UserRole.Owner;

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
            userId,
            userRole
        );

        try
        {
            PrgSaving.Visibility = Visibility.Visible;
            BtnIssueLoan.IsEnabled = false;

            var loan = await _loanService.IssueNewLoanAsync(dto);
            MessageBox.Show($"Jewel Loan #{loan.LoanNumber} successfully issued!\n" +
                            $"Disbursed: {ValidationHelper.FormatCurrency(loan.LoanAmount)}\n" +
                            $"Net Weight: {ValidationHelper.FormatWeight(loan.NetWeight)}\n" +
                            $"Voucher posted: Dr Jewel Loan, Cr Cash",
                "Loan Disbursed Successfully", MessageBoxButton.OK, MessageBoxImage.Information);

            OnClearClick(sender, e);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Loan issuance failed.");
            MessageBox.Show($"Loan issuance failed: {ex.Message}", "Disbursal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PrgSaving.Visibility = Visibility.Collapsed;
            BtnIssueLoan.IsEnabled = true;
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _selectedCustomer = null;
        TxtCustomerSearch.Text = "";
        TxtNominee.Text = "";
        TxtPhotoPath.Text = "";
        TxtOverrideReason.Text = "";
        TxtOverrideNotice.Visibility = Visibility.Collapsed;
        TxtCustomerError.Visibility = Visibility.Collapsed;
        TxtLoanAmountError.Visibility = Visibility.Collapsed;
        TxtOverrideError.Visibility = Visibility.Collapsed;

        Items.Clear();
        Items.Add(new LoanItemRowViewModel
        {
            Description = "22K Gold Chain",
            Quantity = 1,
            GrossWeight = 20.000m,
            DeductionWeight = 1.500m,
            PurityPercent = 91.60m,
            RatePerGram = _today22KRate
        });

        _isProgrammaticAmountChange = true;
        TxtLoanAmount.Text = "0.00";
        _isProgrammaticAmountChange = false;

        RecalculateTotals();
    }
}
