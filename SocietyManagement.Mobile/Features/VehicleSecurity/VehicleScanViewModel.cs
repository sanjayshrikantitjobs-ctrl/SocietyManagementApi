using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.OCR;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.VehicleSecurity;

/// <summary>Manual plate entry -> match flow, mirrors vehicle-scan.component.ts's
/// non-OCR path (confirmAndSearch()) — one call both logs the scan and
/// returns match/no-match. "Scan Plate (Camera)" runs on-device OCR
/// (ML Kit on Android, Vision on iOS via Plugin.Maui.OCR) against the
/// captured photo and prefills the field with the recognized plate —
/// a single-shot capture-then-recognize, not the web's continuous
/// live-video consensus-vote scan (vehicle-live-scan.component.ts), which
/// would need a custom camera-preview overlay and is a larger follow-up.
/// Either way the recognized text is always left editable, never
/// auto-submitted, same as the web's own OCR path.</summary>
public partial class VehicleScanViewModel : ObservableObject
{
    private readonly VehicleScansClient _scansClient;
    private readonly CurrentSocietyService _currentSocietyService;
    private readonly IOcrService _ocrService;

    public VehicleScanViewModel(VehicleScansClient scansClient, CurrentSocietyService currentSocietyService, IOcrService? ocrService = null)
    {
        _scansClient = scansClient;
        _currentSocietyService = currentSocietyService;
        _ocrService = ocrService ?? OcrPlugin.Default;
    }

    [ObservableProperty] private string registrationNumber = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private VehicleScanResultDto? scanResult;
    [ObservableProperty] private string? capturedPhotoPath;

    public ImageSource? CapturedPhotoPreview => string.IsNullOrEmpty(CapturedPhotoPath) ? null : ImageSource.FromFile(CapturedPhotoPath);

    partial void OnCapturedPhotoPathChanged(string? value) => OnPropertyChanged(nameof(CapturedPhotoPreview));

    /// <summary>Opens the camera to photograph the plate, then runs
    /// on-device OCR against that photo and prefills RegistrationNumber
    /// with the best-looking recognized token (see PickPlateCandidate) —
    /// the field stays fully editable either way, so a bad read is just
    /// corrected by hand rather than blocking the scan.</summary>
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
            byte[] imageBytes;
            await using (var sourceStream = await photo.OpenReadAsync())
            {
                using var memoryStream = new MemoryStream();
                await sourceStream.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }
            await File.WriteAllBytesAsync(localPath, imageBytes);
            CapturedPhotoPath = localPath;

            await RecognizePlateAsync(imageBytes);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't capture a photo ({ex.Message}).";
        }
    }

    private bool _ocrInitialized;

    private async Task RecognizePlateAsync(byte[] imageBytes)
    {
        IsBusy = true;
        try
        {
            if (!_ocrInitialized)
            {
                await _ocrService.InitAsync();
                _ocrInitialized = true;
            }

            var result = await _ocrService.RecognizeTextAsync(imageBytes, tryHard: true);
            var candidate = result.Success ? PickPlateCandidate(result.Lines) : null;
            if (candidate is not null)
            {
                RegistrationNumber = candidate;
            }
            else
            {
                ErrorMessage = "Couldn't read a plate in that photo — enter the registration number manually.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"OCR failed ({ex.Message}) — enter the registration number manually.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Indian plates normalize to 9-10 alphanumeric characters
    /// (e.g. MH04AB1234) — picks the recognized line whose normalized form
    /// is closest to that length, since ML Kit/Vision return every line of
    /// text on the plate (state name, "IND" badge, etc.) not just the
    /// number.</summary>
    private static string? PickPlateCandidate(IList<string> lines)
    {
        return lines
            .Select(NormalizePlate)
            .Where(n => n.Length >= 6 && n.Length <= 12)
            .OrderBy(n => Math.Abs(n.Length - 10))
            .FirstOrDefault();
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
