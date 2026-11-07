using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Win32;

namespace JGF.Desktop.Views.Controls;

public partial class CustomerMasterControl : UserControl
{
    private readonly ICustomerService _customerService;
    private readonly Debouncer _searchDebouncer = new(300);
    private Customer? _selectedCustomer;
    private string? _currentPhotoPath;

    public CustomerMasterControl(ICustomerService customerService)
    {
        InitializeComponent();
        _customerService = customerService;
        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadCustomersAsync();
    }

    private async Task LoadCustomersAsync(string search = "", CancellationToken ct = default)
    {
        try
        {
            SetLoading(true);
            var list = await _customerService.SearchCustomersAsync(search, take: 50, ct);
            GridCustomers.ItemsSource = list;
        }
        catch (OperationCanceledException)
        {
            // Debounced or cancelled
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load customers: {ex.Message}", "Customer Search", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        PrgLoading.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        BtnNewCustomer.IsEnabled = !isLoading;
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = TxtSearch.Text;
        await _searchDebouncer.RunAsync(async ct =>
        {
            await LoadCustomersAsync(searchText, ct);
        });
    }

    private void OnCustomerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GridCustomers.SelectedItem is Customer c)
        {
            _selectedCustomer = c;
            TxtCode.Text = c.Code;
            TxtFullName.Text = c.FullName;
            TxtNameTamil.Text = c.NameTamil ?? "";
            TxtGuardian.Text = c.GuardianName ?? "";
            TxtMobile.Text = c.MobileNumber;
            TxtAadhaar.Text = c.AadhaarNumber ?? "";
            TxtPan.Text = c.PanNumber ?? "";
            TxtAddress.Text = c.Address ?? "";
            TxtCity.Text = c.City ?? "";
            TxtDistrict.Text = c.District ?? "";
            TxtPincode.Text = c.Pincode ?? "";

            _currentPhotoPath = c.PhotoPath;
            if (!string.IsNullOrEmpty(c.PhotoPath) && File.Exists(c.PhotoPath))
            {
                ImgCustomerPhoto.Source = new BitmapImage(new Uri(c.PhotoPath));
            }
            else
            {
                ImgCustomerPhoto.Source = null;
            }
        }
    }

    private void OnNewCustomerClick(object sender, RoutedEventArgs e)
    {
        _selectedCustomer = null;
        GridCustomers.SelectedItem = null;
        TxtCode.Text = "(Auto)";
        TxtFullName.Text = "";
        TxtNameTamil.Text = "";
        TxtGuardian.Text = "";
        TxtMobile.Text = "";
        TxtAadhaar.Text = "";
        TxtPan.Text = "";
        TxtAddress.Text = "";
        TxtPincode.Text = "";
        _currentPhotoPath = null;
        ImgCustomerPhoto.Source = null;
        TxtFullName.Focus();
    }

    private void OnCapturePhotoClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Customer Photo",
            Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
        };

        if (ofd.ShowDialog() == true)
        {
            var photosDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomerPhotos");
            Directory.CreateDirectory(photosDir);

            var dest = Path.Combine(photosDir, $"{Guid.NewGuid():N}{Path.GetExtension(ofd.FileName)}");
            File.Copy(ofd.FileName, dest, overwrite: true);
            _currentPhotoPath = dest;
            ImgCustomerPhoto.Source = new BitmapImage(new Uri(dest));
        }
    }

    private async void OnSaveCustomerClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtFullName.Text) || string.IsNullOrWhiteSpace(TxtMobile.Text))
        {
            MessageBox.Show("Please enter mandatory fields: Full Name and Mobile Number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetLoading(true);
            if (_selectedCustomer == null)
            {
                // Create
                var newCustomer = new Customer
                {
                    Code = "C" + DateTime.Now.ToString("yyMMddHHmmss"),
                    FullName = TxtFullName.Text.Trim(),
                    NameTamil = TxtNameTamil.Text.Trim(),
                    GuardianName = TxtGuardian.Text.Trim(),
                    MobileNumber = TxtMobile.Text.Trim(),
                    AadhaarNumber = TxtAadhaar.Text.Trim(),
                    PanNumber = TxtPan.Text.Trim(),
                    Address = TxtAddress.Text.Trim(),
                    City = TxtCity.Text.Trim(),
                    District = TxtDistrict.Text.Trim(),
                    Pincode = TxtPincode.Text.Trim(),
                    PhotoPath = _currentPhotoPath
                };

                await _customerService.CreateCustomerAsync(newCustomer);
                MessageBox.Show($"Customer {newCustomer.FullName} created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Update
                _selectedCustomer.FullName = TxtFullName.Text.Trim();
                _selectedCustomer.NameTamil = TxtNameTamil.Text.Trim();
                _selectedCustomer.GuardianName = TxtGuardian.Text.Trim();
                _selectedCustomer.MobileNumber = TxtMobile.Text.Trim();
                _selectedCustomer.AadhaarNumber = TxtAadhaar.Text.Trim();
                _selectedCustomer.PanNumber = TxtPan.Text.Trim();
                _selectedCustomer.Address = TxtAddress.Text.Trim();
                _selectedCustomer.City = TxtCity.Text.Trim();
                _selectedCustomer.District = TxtDistrict.Text.Trim();
                _selectedCustomer.Pincode = TxtPincode.Text.Trim();
                _selectedCustomer.PhotoPath = _currentPhotoPath;

                await _customerService.UpdateCustomerAsync(_selectedCustomer);
                MessageBox.Show($"Customer {_selectedCustomer.FullName} updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnDeleteCustomerClick(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;

        var confirm = MessageBox.Show($"Are you sure you want to delete customer '{_selectedCustomer.FullName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            try
            {
                SetLoading(true);
                await _customerService.SoftDeleteCustomerAsync(_selectedCustomer.Id);
                OnNewCustomerClick(sender, e);
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetLoading(false);
            }
        }
    }
}
