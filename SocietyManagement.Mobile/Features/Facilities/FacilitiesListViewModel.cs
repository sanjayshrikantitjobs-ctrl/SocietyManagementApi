using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Features.Facilities;

/// <summary>Resident-facing browse list — mirrors facilities-list.component.ts's
/// resident view (Admin facility management stays web-only for this module).</summary>
public partial class FacilitiesListViewModel : ObservableObject
{
    private readonly FacilitiesClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public FacilitiesListViewModel(FacilitiesClient client, CurrentSocietyService currentSocietyService, AuthState authState)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
        Auth = authState;
    }

    public AuthState Auth { get; }

    [ObservableProperty] private ObservableCollection<FacilityDto> facilities = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.FacilitiesGETAsync(societyId, true);
            Facilities = new ObservableCollection<FacilityDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load facilities ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
