using System.Windows;
using System.Windows.Controls;
using JGF.Application.Services;

namespace JGF.Desktop.Views.Controls;

public partial class AuditLogControl : UserControl
{
    private readonly IAuditService _auditService;

    public AuditLogControl(IAuditService auditService)
    {
        InitializeComponent();
        _auditService = auditService;
        DpFrom.SelectedDate = DateTime.Today.AddDays(-7);
        DpTo.SelectedDate = DateTime.Today;

        Loaded += async (s, e) => await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        var selectedItem = (CmbTable.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var table = (selectedItem == "All Tables") ? null : selectedItem;

        var from = DpFrom.SelectedDate?.ToUniversalTime();
        var to = DpTo.SelectedDate?.AddDays(1).ToUniversalTime();

        var logs = await _auditService.QueryAuditLogsAsync(table, null, from, to);
        GridAudit.ItemsSource = logs;
    }

    private async void OnFilterClick(object sender, RoutedEventArgs e)
    {
        await LoadLogsAsync();
    }
}

