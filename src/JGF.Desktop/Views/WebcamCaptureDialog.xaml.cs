using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FlashCap;
using Microsoft.Win32;
using Serilog;

namespace JGF.Desktop.Views;

public partial class WebcamCaptureDialog : Window
{
    private readonly string _targetFolder;
    private readonly string _filePrefix;
    private CaptureDevice? _captureDevice;
    private byte[]? _latestFrameBytes;
    private byte[]? _capturedSnapshotBytes;
    private List<CaptureDeviceDescriptor> _descriptors = new();
    private volatile bool _isStreaming = false;

    public string? CapturedFilePath { get; private set; }

    public WebcamCaptureDialog(string targetFolder, string filePrefix = "PHOTO")
    {
        InitializeComponent();
        _targetFolder = targetFolder;
        _filePrefix = filePrefix;

        try
        {
            Directory.CreateDirectory(_targetFolder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create target directory {Dir}", _targetFolder);
        }

        Loaded += async (s, e) => await InitializeCamerasAsync();
        Closing += async (s, e) => await StopCameraAsync();
    }

    private async Task InitializeCamerasAsync()
    {
        try
        {
            PnlCameraStatus.Visibility = Visibility.Visible;
            TxtCameraStatus.Text = "Detecting video cameras...";

            var devices = new CaptureDevices();
            _descriptors = devices.EnumerateDescriptors().ToList();

            CmbCameras.Items.Clear();
            if (_descriptors.Count == 0)
            {
                TxtCameraStatus.Text = "No webcam detected on this PC.\nYou can browse and select an existing image file.";
                BtnCapture.IsEnabled = false;
                return;
            }

            foreach (var d in _descriptors)
            {
                CmbCameras.Items.Add(d.Name);
            }

            CmbCameras.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error enumerating capture devices.");
            TxtCameraStatus.Text = $"Camera initialization error: {ex.Message}";
            BtnCapture.IsEnabled = false;
        }
    }

    private async void OnCameraSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbCameras.SelectedIndex < 0 || CmbCameras.SelectedIndex >= _descriptors.Count)
            return;

        var selectedDescriptor = _descriptors[CmbCameras.SelectedIndex];
        await StartCameraAsync(selectedDescriptor);
    }

    private async Task StartCameraAsync(CaptureDeviceDescriptor descriptor)
    {
        await StopCameraAsync();

        try
        {
            TxtCameraStatus.Text = "Starting camera feed...";
            PnlCameraStatus.Visibility = Visibility.Visible;

            // Pick standard video characteristic (approx 640x480 or 1280x720)
            var characteristics = descriptor.Characteristics;
            var characteristic = characteristics
                .OrderByDescending(c => c.Width * c.Height)
                .FirstOrDefault(c => c.Width <= 1280 && c.Height <= 720)
                ?? characteristics.FirstOrDefault();

            if (characteristic == null)
            {
                TxtCameraStatus.Text = "No compatible camera stream characteristics found.";
                return;
            }

            _captureDevice = await descriptor.OpenAsync(characteristic, async bufferScope =>
            {
                if (!_isStreaming) return;

                byte[] imageBytes = bufferScope.Buffer.ExtractImage();
                _latestFrameBytes = imageBytes;

                // Dispatch to UI
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!_isStreaming) return;
                        using var ms = new MemoryStream(imageBytes);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();

                        ImgLivePreview.Source = bmp;
                        if (PnlCameraStatus.Visibility == Visibility.Visible)
                        {
                            PnlCameraStatus.Visibility = Visibility.Collapsed;
                        }
                    });
                }
                catch
                {
                    // Frame drop / closing
                }
            });

            _isStreaming = true;
            await _captureDevice.StartAsync();
            BtnCapture.IsEnabled = true;
            TxtInfo.Text = "Camera is live. Position the subject and click 'Take Photo'.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start capture device.");
            TxtCameraStatus.Text = $"Could not start camera: {ex.Message}";
            PnlCameraStatus.Visibility = Visibility.Visible;
            BtnCapture.IsEnabled = false;
        }
    }

    private async Task StopCameraAsync()
    {
        _isStreaming = false;
        if (_captureDevice != null)
        {
            try
            {
                await _captureDevice.StopAsync();
                await _captureDevice.DisposeAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while stopping capture device.");
            }
            finally
            {
                _captureDevice = null;
            }
        }
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        if (_latestFrameBytes == null || _latestFrameBytes.Length == 0)
        {
            MessageBox.Show("No camera frame available to capture yet.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _capturedSnapshotBytes = (byte[])_latestFrameBytes.Clone();
        _isStreaming = false;

        // Show frozen snapshot
        using var ms = new MemoryStream(_capturedSnapshotBytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();

        ImgSnapshot.Source = bmp;
        ImgSnapshot.Visibility = Visibility.Visible;
        ImgLivePreview.Visibility = Visibility.Collapsed;
        BdrGuide.Visibility = Visibility.Collapsed;

        BtnCapture.Visibility = Visibility.Collapsed;
        BtnRetake.Visibility = Visibility.Visible;
        BtnAccept.Visibility = Visibility.Visible;

        TxtInfo.Text = "Photo captured! Click 'Use Photo' to save or 'Retake' to try again.";
    }

    private void OnRetakeClick(object sender, RoutedEventArgs e)
    {
        _capturedSnapshotBytes = null;
        ImgSnapshot.Visibility = Visibility.Collapsed;
        ImgSnapshot.Source = null;
        ImgLivePreview.Visibility = Visibility.Visible;
        BdrGuide.Visibility = Visibility.Visible;

        BtnCapture.Visibility = Visibility.Visible;
        BtnRetake.Visibility = Visibility.Collapsed;
        BtnAccept.Visibility = Visibility.Collapsed;

        _isStreaming = true;
        TxtInfo.Text = "Camera is live. Position the subject and click 'Take Photo'.";
    }

    private async void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (_capturedSnapshotBytes == null || _capturedSnapshotBytes.Length == 0)
        {
            MessageBox.Show("No photo captured.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            var fileName = $"{_filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.jpg";
            var fullPath = Path.Combine(_targetFolder, fileName);
            await File.WriteAllBytesAsync(fullPath, _capturedSnapshotBytes);

            CapturedFilePath = fullPath;
            await StopCameraAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save captured photo.");
            MessageBox.Show($"Failed to save photo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnBrowseFileClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Image File",
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var ext = Path.GetExtension(ofd.FileName);
                var fileName = $"{_filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(_targetFolder, fileName);
                File.Copy(ofd.FileName, fullPath, overwrite: true);

                CapturedFilePath = fullPath;
                await StopCameraAsync();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to copy chosen image.");
                MessageBox.Show($"Failed to select photo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OnCancelClick(object sender, RoutedEventArgs e)
    {
        await StopCameraAsync();
        DialogResult = false;
        Close();
    }
}
