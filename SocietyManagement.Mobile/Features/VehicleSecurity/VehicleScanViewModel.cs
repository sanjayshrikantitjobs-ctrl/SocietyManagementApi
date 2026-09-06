using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.VehicleSecurity;

/// <summary>Manual plate entry -> match flow, mirrors vehicle-scan.component.ts's
/// non-OCR path (confirmAndSearch()) — one call both logs the scan and
/// returns match/no-match. Live camera OCR (vehicle-live-scan.component.ts's
/// Tesseract.js-based continuous scan) is a separate, larger piece using
/// on-device ML Kit/Vision, tracked as its own follow-up rather than bundled
/// here — this page is the always-available manual fallback either way.</summary>
public partial class VehicleScanViewModel : ObservableObject
{
    private readonly VehicleScansClient _scansClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public VehicleScanViewModel(VehicleScansClient scansClient, CurrentSocietyService currentSocietyService)
    {
        _scansClient = scansClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private string registrationNumber = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private VehicleScanResultDto? scanResult;
    [ObservableProperty] private string? capturedPhotoPath;

    public ImageSource? CapturedPhotoPreview => string.IsNullOrEmpty(CapturedPhotoPath) ? null : ImageSource.FromFile(CapturedPhotoPath);

    partial void OnCapturedPhotoPathChanged(string? value) => OnPropertyChanged(nameof(CapturedPhotoPreview));

    /// <summary>Opens the camera to photograph the plate for the scan
    /// record — full on-device OCR (guide box, continuous recognition,
    /// consensus voting, matching vehicle-live-scan.component.ts) is a
    /// separate, larger follow-up; this gives the button a real camera
    /// capture today, with the plate still typed/confirmed manually
    /// below and the photo attached as evidence (Source becomes
    /// OcrCamera once a photo is attached, matching how the web tags a
    /// scan that went through the camera flow vs. pure manual search).</summary>
    [RelayCommand]
    private async Task CapturePhotoAsync()
    {
        ErrorMessage = null;
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                ErrorMessage = "Camera capture isn't supported on this device.";
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null) return;

            var localPath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
            await using (var sourceStream = await photo.OpenReadAsync())
            await using (var localFileStream = File.Create(localPath))
            {
                await sourceStream.CopyToAsync(localFileStream);
            }
            CapturedPhotoPath = localPath;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't capture a photo ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null)
        {
            ErrorMessage = "No society available for this account.";
            return;
        }
        var normalized = NormalizePlate(RegistrationNumber);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            ErrorMessage = "Enter the registration number.";
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            byte[]? imageBytes = null;
            if (!string.IsNullOrEmpty(CapturedPhotoPath) && File.Exists(CapturedPhotoPath))
                imageBytes = await File.ReadAllBytesAsync(CapturedPhotoPath);

            var response = await _scansClient.ConfirmAsync(new ConfirmVehicleScanRequest
            {
                SocietyId = societyId,
                NormalizedRegistrationNumber = normalized,
                RawOcrText = null,
                Confidence = null,
                Source = imageBytes is null ? VehicleScanSource.ManualSearch : VehicleScanSource.OcrCamera,
                GateId = null,
                ImageBytes = imageBytes
            });
            ScanResult = response.Data;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't check this plate ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        RegistrationNumber = string.Empty;
        ScanResult = null;
        ErrorMessage = null;
        CapturedPhotoPath = null;
    }

    /// <summary>Mirrors the backend's VehicleNumberNormalizer.Normalize —
    /// strips everything but letters/digits and uppercases.</summary>
    private static string NormalizePlate(string text) =>
        new string(text.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
}
