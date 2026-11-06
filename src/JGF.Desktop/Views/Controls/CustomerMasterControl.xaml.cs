using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using Microsoft.Win32;

namespace JGF.Desktop.Views.Controls;

public partial class CustomerMasterControl : UserControl
{
    private readonly ICustomerService _customerService;
    private Customer? _selectedCustomer;
    private string? _currentPhotoPath;

    public CustomerMasterControl(ICustomerService customerService)
    {
        InitializeComponent();
        _customerService = customerService;
        Loaded += async (s, e) => await LoadCustomersAsync();
    }

    private async Task LoadCustomersAsync(string search = "")
    {
        var list = await _customerService.SearchCustomersAsync(search);
        GridCustomers.ItemsSource = list;
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        await LoadCustomersAsync(TxtSearch.Text);
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
            MessageBox.Show("Customer Name and Mobile Number are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedCustomer == null)
        {
            var customer = new Customer
            {
                Code = $"C{DateTime.UtcNow:MMddHHmm}",
                FullName = TxtFullName.Text.Trim(),
                NameTamil = TxtNameTamil.Text.Trim(),
                GuardianName = TxtGuardian.Text.Trim(),
                Gender = Gender.Male,
                MobileNumber = TxtMobile.Text.Trim(),
                AadhaarNumber = TxtAadhaar.Text.Trim(),
                PanNumber = TxtPan.Text.Trim(),
                Address = TxtAddress.Text.Trim(),
                City = TxtCity.Text.Trim(),
                District = TxtDistrict.Text.Trim(),
                Pincode = TxtPincode.Text.Trim(),
                PhotoPath = _currentPhotoPath
            };

            await _customerService.CreateCustomerAsync(customer);
            MessageBox.Show("Customer saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
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
            MessageBox.Show("Customer updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        await LoadCustomersAsync();
    }

    private async void OnDeleteCustomerClick(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;

        var confirm = MessageBox.Show($"Are you sure you want to soft delete customer '{_selectedCustomer.FullName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            await _customerService.SoftDeleteCustomerAsync(_selectedCustomer.Id);
            OnNewCustomerClick(sender, e);
            await LoadCustomersAsync();
        }
    }
}
