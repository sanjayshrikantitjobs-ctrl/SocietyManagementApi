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
            var response = await _scansClient.ConfirmAsync(new ConfirmVehicleScanRequest
            {
                SocietyId = societyId,
                NormalizedRegistrationNumber = normalized,
                RawOcrText = null,
                Confidence = null,
                Source = VehicleScanSource.ManualSearch,
                GateId = null,
                ImageBytes = null
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
    }

    /// <summary>Mirrors the backend's VehicleNumberNormalizer.Normalize —
    /// strips everything but letters/digits and uppercases.</summary>
    private static string NormalizePlate(string text) =>
        new string(text.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
}
