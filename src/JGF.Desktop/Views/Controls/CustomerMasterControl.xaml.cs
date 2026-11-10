using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using JGF.Application.Services;
using JGF.Desktop.Common;
using JGF.Domain.Entities;
using Microsoft.Win32;
using Serilog;

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
            Log.Error(ex, "Failed to load customer list.");
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
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(c.PhotoPath);
                    bmp.EndInit();
                    bmp.Freeze();
                    ImgCustomerPhoto.Source = bmp;
                }
                catch
                {
                    ImgCustomerPhoto.Source = null;
                }
            }
            else
            {
                ImgCustomerPhoto.Source = null;
            }

            BtnDeleteCustomer.Visibility = Visibility.Visible;
            ValidateAll();
        }
    }

    private void OnNewCustomerClick(object sender, RoutedEventArgs e)
    {
        _selectedCustomer = null;
        GridCustomers.SelectedItem = null;
        TxtCode.Text = "(Auto-Generated)";
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
        BtnDeleteCustomer.Visibility = Visibility.Collapsed;
        ClearValidationErrors();
        TxtFullName.Focus();
    }

    private void OnCapturePhotoClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var photosDir = Path.Combine(programData, "JGF", "CustomerPhotos");
            Directory.CreateDirectory(photosDir);

            var dialog = new WebcamCaptureDialog(photosDir, "CUST")
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.CapturedFilePath))
            {
                _currentPhotoPath = dialog.CapturedFilePath;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(dialog.CapturedFilePath);
                bmp.EndInit();
                bmp.Freeze();
                ImgCustomerPhoto.Source = bmp;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture customer photo.");
            MessageBox.Show($"Camera error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnFullNameTextChanged(object sender, TextChangedEventArgs e) => ValidateFullName();
    private void OnMobileTextChanged(object sender, TextChangedEventArgs e) => ValidateMobile();
    private void OnAadhaarTextChanged(object sender, TextChangedEventArgs e) => ValidateAadhaar();
    private void OnPanTextChanged(object sender, TextChangedEventArgs e) => ValidatePan();
    private void OnPincodeTextChanged(object sender, TextChangedEventArgs e) => ValidatePincode();

    private bool ValidateFullName()
    {
        var valid = !string.IsNullOrWhiteSpace(TxtFullName.Text) && TxtFullName.Text.Trim().Length >= 2;
        TxtNameError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        return valid;
    }

    private bool ValidateMobile()
    {
        var valid = ValidationHelper.IsValidMobile(TxtMobile.Text);
        TxtMobileError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        return valid;
    }

    private bool ValidateAadhaar()
    {
        var valid = ValidationHelper.IsValidAadhaar(TxtAadhaar.Text);
        TxtAadhaarError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        return valid;
    }

    private bool ValidatePan()
    {
        var valid = ValidationHelper.IsValidPan(TxtPan.Text);
        TxtPanError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        return valid;
    }

    private bool ValidatePincode()
    {
        var valid = ValidationHelper.IsValidPincode(TxtPincode.Text);
        TxtPincodeError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        return valid;
    }

    private bool ValidateAll()
    {
        bool a = ValidateFullName();
        bool b = ValidateMobile();
        bool c = ValidateAadhaar();
        bool d = ValidatePan();
        bool e = ValidatePincode();
        return a && b && c && d && e;
    }

    private void ClearValidationErrors()
    {
        TxtNameError.Visibility = Visibility.Collapsed;
        TxtMobileError.Visibility = Visibility.Collapsed;
        TxtAadhaarError.Visibility = Visibility.Collapsed;
        TxtPanError.Visibility = Visibility.Collapsed;
        TxtPincodeError.Visibility = Visibility.Collapsed;
    }

    private async void OnSaveCustomerClick(object sender, RoutedEventArgs e)
    {
        if (!ValidateAll())
        {
            MessageBox.Show("Please fix the validation errors marked in red before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    PanNumber = TxtPan.Text.Trim().ToUpperInvariant(),
                    Address = TxtAddress.Text.Trim(),
                    City = TxtCity.Text.Trim(),
                    District = TxtDistrict.Text.Trim(),
                    Pincode = TxtPincode.Text.Trim(),
                    PhotoPath = _currentPhotoPath
                };

                await _customerService.CreateCustomerAsync(newCustomer);
                MessageBox.Show($"Customer '{newCustomer.FullName}' created successfully with Code: {newCustomer.Code}", "Customer Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Update
                _selectedCustomer.FullName = TxtFullName.Text.Trim();
                _selectedCustomer.NameTamil = TxtNameTamil.Text.Trim();
                _selectedCustomer.GuardianName = TxtGuardian.Text.Trim();
                _selectedCustomer.MobileNumber = TxtMobile.Text.Trim();
                _selectedCustomer.AadhaarNumber = TxtAadhaar.Text.Trim();
                _selectedCustomer.PanNumber = TxtPan.Text.Trim().ToUpperInvariant();
                _selectedCustomer.Address = TxtAddress.Text.Trim();
                _selectedCustomer.City = TxtCity.Text.Trim();
                _selectedCustomer.District = TxtDistrict.Text.Trim();
                _selectedCustomer.Pincode = TxtPincode.Text.Trim();
                _selectedCustomer.PhotoPath = _currentPhotoPath;

                await _customerService.UpdateCustomerAsync(_selectedCustomer);
                MessageBox.Show($"Customer '{_selectedCustomer.FullName}' updated successfully!", "Customer Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await LoadCustomersAsync();
            OnNewCustomerClick(sender, e);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save customer.");
            MessageBox.Show($"Failed to save customer: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnDeleteCustomerClick(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;

        var confirm = MessageBox.Show($"Are you sure you want to soft delete customer '{_selectedCustomer.FullName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            try
            {
                SetLoading(true);
                await _customerService.SoftDeleteCustomerAsync(_selectedCustomer.Id);
                MessageBox.Show("Customer soft-deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                OnNewCustomerClick(sender, e);
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete customer.");
                MessageBox.Show($"Failed to delete customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetLoading(false);
            }
        }
    }
}
