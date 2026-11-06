using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;
using JGF.Domain.Entities;

namespace JGF.Desktop.Views.Controls;

public partial class JewelTypeMasterControl : UserControl
{
    private readonly IJewelTypeService _jewelService;
    private JewelType? _selected;

    public JewelTypeMasterControl(IJewelTypeService jewelService)
    {
        InitializeComponent();
        _jewelService = jewelService;
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _jewelService.GetAllJewelTypesAsync();
        GridJewelTypes.ItemsSource = list;
    }

    private void OnTypeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GridJewelTypes.SelectedItem is JewelType jt)
        {
            _selected = jt;
            TxtCode.Text = jt.Code;
            TxtName.Text = jt.Name;
            TxtDesc.Text = jt.Description ?? "";
        }
    }

    private void OnNewTypeClick(object sender, RoutedEventArgs e)
    {
        _selected = null;
        GridJewelTypes.SelectedItem = null;
        TxtCode.Text = "";
        TxtName.Text = "";
        TxtDesc.Text = "";
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCode.Text) || string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Code and Name are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selected == null)
        {
            var jt = new JewelType
            {
                Code = TxtCode.Text.Trim(),
                Name = TxtName.Text.Trim(),
                Description = TxtDesc.Text.Trim(),
                IsActive = true
            };
            await _jewelService.CreateJewelTypeAsync(jt);
        }
        else
        {
            _selected.Code = TxtCode.Text.Trim();
            _selected.Name = TxtName.Text.Trim();
            _selected.Description = TxtDesc.Text.Trim();
            await _jewelService.UpdateJewelTypeAsync(_selected);
        }

        MessageBox.Show("Saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        await LoadAsync();
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (MessageBox.Show($"Delete '{_selected.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            await _jewelService.SoftDeleteJewelTypeAsync(_selected.Id);
            OnNewTypeClick(sender, e);
            await LoadAsync();
        }
    }
}

