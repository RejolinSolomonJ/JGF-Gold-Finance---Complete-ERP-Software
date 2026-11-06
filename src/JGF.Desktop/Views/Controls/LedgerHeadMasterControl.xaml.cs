using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Desktop.Views.Controls;

public partial class LedgerHeadMasterControl : UserControl
{
    private readonly IAccountingService _accService;
    private LedgerHead? _selected;

    public LedgerHeadMasterControl(IAccountingService accService)
    {
        InitializeComponent();
        _accService = accService;
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _accService.GetAllLedgerHeadsAsync();
        GridHeads.ItemsSource = list;
    }

    private void OnHeadSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GridHeads.SelectedItem is LedgerHead lh)
        {
            _selected = lh;
            TxtCode.Text = lh.Code;
            TxtName.Text = lh.Name;
            TxtDesc.Text = lh.Description ?? "";
            CmbGroup.Text = lh.AccountGroup.ToString();
            CmbBalance.Text = lh.NormalBalance;
        }
    }

    private void OnNewHeadClick(object sender, RoutedEventArgs e)
    {
        _selected = null;
        GridHeads.SelectedItem = null;
        TxtCode.Text = "";
        TxtName.Text = "";
        TxtDesc.Text = "";
    }

    private async void OnSaveHeadClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCode.Text) || string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Account Code and Name are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Enum.TryParse<LedgerHeadType>(CmbGroup.Text, out var group);

        if (_selected == null)
        {
            var head = new LedgerHead
            {
                Code = TxtCode.Text.Trim(),
                Name = TxtName.Text.Trim(),
                AccountGroup = group,
                NormalBalance = CmbBalance.Text,
                Description = TxtDesc.Text.Trim()
            };
            await _accService.CreateLedgerHeadAsync(head);
        }
        else
        {
            _selected.Code = TxtCode.Text.Trim();
            _selected.Name = TxtName.Text.Trim();
            _selected.AccountGroup = group;
            _selected.NormalBalance = CmbBalance.Text;
            _selected.Description = TxtDesc.Text.Trim();
            await _accService.UpdateLedgerHeadAsync(_selected);
        }

        MessageBox.Show("Saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        await LoadAsync();
    }

    private async void OnDeleteHeadClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (_selected.IsSystemAccount)
        {
            MessageBox.Show("System accounts cannot be deleted.", "Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Delete ledger head '{_selected.Name}'?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            await _accService.SoftDeleteLedgerHeadAsync(_selected.Id);
            OnNewHeadClick(sender, e);
            await LoadAsync();
        }
    }
}

