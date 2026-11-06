using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;
using JGF.Domain.Entities;

namespace JGF.Desktop.Views.Controls;

public partial class GoldRateMasterControl : UserControl
{
    private readonly IGoldRateService _goldService;

    public GoldRateMasterControl(IGoldRateService goldService)
    {
        InitializeComponent();
        _goldService = goldService;
        DpDate.SelectedDate = DateTime.Today;
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var rates = await _goldService.GetRecentRatesAsync(30);
        GridRates.ItemsSource = rates;

        var todayRate = await _goldService.GetTodayRateAsync();
        if (todayRate != null)
        {
            Txt22KRate.Text = todayRate.Rate22K.ToString("0.00");
            Txt18KRate.Text = (todayRate.Rate18K ?? 0).ToString("0.00");
            TxtMarketRate.Text = todayRate.RatePerGram.ToString("0.00");
            TxtSource.Text = todayRate.Source ?? "";
        }
    }

    private async void OnSaveRateClick(object sender, RoutedEventArgs e)
    {
        decimal.TryParse(Txt22KRate.Text, out var r22);
        decimal.TryParse(Txt18KRate.Text, out var r18);
        decimal.TryParse(TxtMarketRate.Text, out var rm);

        if (r22 <= 0)
        {
            MessageBox.Show("Please enter a valid 22K gold rate.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var rate = new GoldRate
        {
            Date = DateOnly.FromDateTime(DpDate.SelectedDate ?? DateTime.Today),
            Rate22K = r22,
            Rate18K = r18,
            RatePerGram = rm > 0 ? rm : r22,
            Source = TxtSource.Text.Trim()
        };

        await _goldService.SetDailyRateAsync(rate);
        MessageBox.Show("Gold rate saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        await LoadAsync();
    }
}

