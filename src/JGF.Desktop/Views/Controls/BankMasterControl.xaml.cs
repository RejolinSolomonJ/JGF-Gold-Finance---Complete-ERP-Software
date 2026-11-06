using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;
using JGF.Domain.Entities;

namespace JGF.Desktop.Views.Controls;

public partial class BankMasterControl : UserControl
{
    private readonly IBankService _bankService;
    private Bank? _selectedBank;

    public BankMasterControl(IBankService bankService)
    {
        InitializeComponent();
        _bankService = bankService;
        Loaded += async (s, e) => await LoadBanksAsync();
    }

    private async Task LoadBanksAsync()
    {
        var list = await _bankService.GetAllBanksAsync();
        GridBanks.ItemsSource = list;
    }

    private void OnBankSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GridBanks.SelectedItem is Bank b)
        {
            _selectedBank = b;
            TxtBankCode.Text = b.Code;
            TxtBankName.Text = b.Name;
            TxtBranch.Text = b.Branch ?? "";
            TxtAccountNo.Text = b.AccountNumber ?? "";
            TxtIfsc.Text = b.IfscCode ?? "";
            TxtContactPerson.Text = b.ContactPerson ?? "";
            TxtPhone.Text = b.ContactPhone ?? "";
            TxtInterestRate.Text = b.InterestRate.ToString("0.00");
            TxtSanctionedLimit.Text = b.SanctionedLimit.ToString("0.00");
        }
    }

    private void OnNewBankClick(object sender, RoutedEventArgs e)
    {
        _selectedBank = null;
        GridBanks.SelectedItem = null;
        TxtBankCode.Text = "";
        TxtBankName.Text = "";
        TxtBranch.Text = "";
        TxtAccountNo.Text = "";
        TxtIfsc.Text = "";
        TxtContactPerson.Text = "";
        TxtPhone.Text = "";
        TxtInterestRate.Text = "0.75";
        TxtSanctionedLimit.Text = "0.00";
    }

    private async void OnSaveBankClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtBankCode.Text) || string.IsNullOrWhiteSpace(TxtBankName.Text))
        {
            MessageBox.Show("Bank Code and Name are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtInterestRate.Text, out var rate);
        decimal.TryParse(TxtSanctionedLimit.Text, out var limit);

        if (_selectedBank == null)
        {
            var b = new Bank
            {
                Code = TxtBankCode.Text.Trim(),
                Name = TxtBankName.Text.Trim(),
                Branch = TxtBranch.Text.Trim(),
                AccountNumber = TxtAccountNo.Text.Trim(),
                IfscCode = TxtIfsc.Text.Trim(),
                ContactPerson = TxtContactPerson.Text.Trim(),
                ContactPhone = TxtPhone.Text.Trim(),
                InterestRate = rate,
                SanctionedLimit = limit,
                IsActive = true
            };
            await _bankService.CreateBankAsync(b);
        }
        else
        {
            _selectedBank.Code = TxtBankCode.Text.Trim();
            _selectedBank.Name = TxtBankName.Text.Trim();
            _selectedBank.Branch = TxtBranch.Text.Trim();
            _selectedBank.AccountNumber = TxtAccountNo.Text.Trim();
            _selectedBank.IfscCode = TxtIfsc.Text.Trim();
            _selectedBank.ContactPerson = TxtContactPerson.Text.Trim();
            _selectedBank.ContactPhone = TxtPhone.Text.Trim();
            _selectedBank.InterestRate = rate;
            _selectedBank.SanctionedLimit = limit;
            await _bankService.UpdateBankAsync(_selectedBank);
        }

        MessageBox.Show("Bank saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        await LoadBanksAsync();
    }

    private async void OnDeleteBankClick(object sender, RoutedEventArgs e)
    {
        if (_selectedBank == null) return;
        if (MessageBox.Show($"Delete bank '{_selectedBank.Name}'?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            await _bankService.SoftDeleteBankAsync(_selectedBank.Id);
            OnNewBankClick(sender, e);
            await LoadBanksAsync();
        }
    }
}

