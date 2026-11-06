using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;
using JGF.Domain.Entities;

namespace JGF.Desktop.Views.Controls;

public partial class NumberSeriesMasterControl : UserControl
{
    private readonly INumberSeriesService _seriesService;

    public NumberSeriesMasterControl(INumberSeriesService seriesService)
    {
        InitializeComponent();
        _seriesService = seriesService;
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _seriesService.GetAllSeriesAsync();
        GridSeries.ItemsSource = list;
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (GridSeries.ItemsSource is IEnumerable<NumberSeries> seriesList)
        {
            foreach (var s in seriesList)
            {
                await _seriesService.CreateOrUpdateSeriesAsync(s);
            }
            MessageBox.Show("Number series updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadAsync();
        }
    }
}

