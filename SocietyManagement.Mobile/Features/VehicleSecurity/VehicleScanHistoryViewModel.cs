using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.VehicleSecurity;

/// <summary>Mirrors vehicle-scan-history.component.ts — every scan this
/// watchman (or, for Admin, the whole society) has logged.</summary>
public partial class VehicleScanHistoryViewModel : ObservableObject
{
    private readonly VehicleScansClient _scansClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public VehicleScanHistoryViewModel(VehicleScansClient scansClient, CurrentSocietyService currentSocietyService)
    {
        _scansClient = scansClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private ObservableCollection<VehicleScanHistoryDto> history = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null)
        {
            ErrorMessage = "No society available for this account.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _scansClient.History2Async(societyId, null, null, null, 1, 50);
            History = new ObservableCollection<VehicleScanHistoryDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load scan history ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
