using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using Serilog;

namespace JGF.Desktop.Views;

public partial class CustomerLookupDialog : Window
{
    private readonly ICustomerService _customerService;
    private readonly Debouncer _debouncer = new(300);

    public Customer? SelectedCustomer { get; private set; }

    public CustomerLookupDialog(ICustomerService customerService, string initialQuery = "")
    {
        InitializeComponent();
        _customerService = customerService;
        Loaded += async (s, e) =>
        {
            TxtSearch.Text = initialQuery;
            TxtSearch.Focus();
            await SearchAsync(initialQuery);
        };
    }

    private async Task SearchAsync(string query, CancellationToken ct = default)
    {
        try
        {
            PrgLoading.Visibility = Visibility.Visible;
            TxtEmptyState.Visibility = Visibility.Collapsed;
            var list = await _customerService.SearchCustomersAsync(query, take: 50, ct);
            GridCustomers.ItemsSource = list;
            TxtEmptyState.Visibility = (list.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

            // Automatically select first row so user can immediately press Enter or click Select Customer
            if (list.Count > 0)
            {
                GridCustomers.SelectedIndex = 0;
            }
        }
        catch (OperationCanceledException)
        {
            // Debounced
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to search customers.");
            MessageBox.Show($"Search failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PrgLoading.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = TxtSearch.Text;
        await _debouncer.RunAsync(async ct =>
        {
            await SearchAsync(text, ct);
        });
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        await SearchAsync(TxtSearch.Text);
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (GridCustomers.SelectedItem != null || GridCustomers.Items.Count > 0)
            {
                ConfirmSelection();
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Down && GridCustomers.Items.Count > 0)
        {
            GridCustomers.Focus();
            GridCustomers.SelectedIndex = 0;
            e.Handled = true;
        }
    }

    private void OnGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var row = FindVisualParent<DataGridRow>(dep);
            if (row?.Item is Customer c)
            {
                row.IsSelected = true;
                GridCustomers.SelectedItem = c;
            }
        }
    }

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmSelection();
            e.Handled = true;
        }
    }

    private void OnCustomerDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var row = FindVisualParent<DataGridRow>(dep);
            if (row?.Item is Customer c)
            {
                SelectedCustomer = c;
                DialogResult = true;
                Close();
                return;
            }
        }

        ConfirmSelection();
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void ConfirmSelection()
    {
        // 1. Check direct SelectedItem
        if (GridCustomers.SelectedItem is Customer c)
        {
            SelectedCustomer = c;
            DialogResult = true;
            Close();
            return;
        }

        // 2. Check CurrentItem
        if (GridCustomers.CurrentItem is Customer cur)
        {
            SelectedCustomer = cur;
            DialogResult = true;
            Close();
            return;
        }

        // 3. Fallback: If only 1 customer or first customer in list exists, take it!
        if (GridCustomers.Items.Count > 0 && GridCustomers.Items[0] is Customer firstCustomer)
        {
            SelectedCustomer = firstCustomer;
            DialogResult = true;
            Close();
            return;
        }

        MessageBox.Show("Please select a customer from the table.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObj = VisualTreeHelper.GetParent(child);
        if (parentObj == null) return null;
        if (parentObj is T parent) return parent;
        return FindVisualParent<T>(parentObj);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

