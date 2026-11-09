using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public partial class SchemeMasterControl : UserControl
{
    private readonly ISchemeService _schemeService;
    private Scheme? _selectedScheme;

    public SchemeMasterControl(ISchemeService schemeService)
    {
        InitializeComponent();
        _schemeService = schemeService;
        Loaded += async (s, e) => await LoadSchemesAsync();
    }

    private async Task LoadSchemesAsync()
    {
        var list = await _schemeService.GetAllSchemesAsync();
        GridSchemes.ItemsSource = list;
    }

    private void OnSchemeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GridSchemes.SelectedItem is Scheme s)
        {
            _selectedScheme = s;
            TxtSchemeCode.Text = s.Code;
            TxtSchemeName.Text = s.Name;
            TxtInterestRate.Text = s.InterestRate.ToString("0.00");
            TxtPenalRate.Text = s.PenalRate.ToString("0.00");
            TxtMaxLtv.Text = s.MaxLtvPercentage.ToString("0.00");
            TxtTenureMonths.Text = s.TenureMonths.ToString();
            TxtGraceDays.Text = s.GracePeriodDays.ToString();
        }
    }

    private void OnNewSchemeClick(object sender, RoutedEventArgs e)
    {
        _selectedScheme = null;
        GridSchemes.SelectedItem = null;
        TxtSchemeCode.Text = "";
        TxtSchemeName.Text = "";
        TxtInterestRate.Text = "1.50";
        TxtPenalRate.Text = "0.50";
        TxtMaxLtv.Text = "75.00";
        TxtTenureMonths.Text = "12";
        TxtGraceDays.Text = "7";
    }

    private async void OnSaveSchemeClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSchemeCode.Text) || string.IsNullOrWhiteSpace(TxtSchemeName.Text))
        {
            MessageBox.Show("Scheme Code and Name are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtInterestRate.Text, out var rate) || rate <= 0)
        {
            MessageBox.Show("Monthly Interest Rate must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtPenalRate.Text, out var penal) || penal < 0)
        {
            MessageBox.Show("Penal Rate cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtMaxLtv.Text, out var ltv) || ltv <= 0 || ltv > 100)
        {
            MessageBox.Show("Max LTV % must be between 1.00% and 100.00%.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TxtTenureMonths.Text, out var tenure) || tenure < 1 || tenure > 60)
        {
            MessageBox.Show("Tenure must be between 1 and 60 months.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TxtGraceDays.Text, out var grace) || grace < 0)
        {
            MessageBox.Show("Grace Days cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedScheme == null)
        {
            var scheme = new Scheme
            {
                Code = TxtSchemeCode.Text.Trim(),
                Name = TxtSchemeName.Text.Trim(),
                InterestMethod = InterestMethod.Simple,
                InterestRate = rate,
                InterestRatePerMonth = rate,
                PenalRate = penal,
                MaxLtvPercentage = ltv,
                TenureMonths = tenure > 0 ? tenure : 12,
                GracePeriodDays = grace
            };
            await _schemeService.CreateSchemeAsync(scheme);
            MessageBox.Show("Scheme created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            _selectedScheme.Code = TxtSchemeCode.Text.Trim();
            _selectedScheme.Name = TxtSchemeName.Text.Trim();
            _selectedScheme.InterestRate = rate;
            _selectedScheme.InterestRatePerMonth = rate;
            _selectedScheme.PenalRate = penal;
            _selectedScheme.MaxLtvPercentage = ltv;
            _selectedScheme.TenureMonths = tenure > 0 ? tenure : 12;
            _selectedScheme.GracePeriodDays = grace;

            await _schemeService.UpdateSchemeAsync(_selectedScheme);
            MessageBox.Show("Scheme updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        await LoadSchemesAsync();
    }

    private async void OnDeleteSchemeClick(object sender, RoutedEventArgs e)
    {
        if (_selectedScheme == null) return;
        var confirm = MessageBox.Show($"Soft delete scheme '{_selectedScheme.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            await _schemeService.SoftDeleteSchemeAsync(_selectedScheme.Id);
            OnNewSchemeClick(sender, e);
            await LoadSchemesAsync();
        }
    }
}

