using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Services;

/// <summary>Read-only pass mirroring the web's Services list (vendor/AMC
/// contracts). Add/Edit/Delete are a follow-up.</summary>
public partial class ServicesViewModel : ObservableObject
{
    private readonly ServicesClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public ServicesViewModel(ServicesClient client, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private ObservableCollection<SocietyServiceDto> services = new();
    [ObservableProperty] private string search = string.Empty;
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
            var response = await _client.ServicesGETAsync(
                societyId, string.IsNullOrWhiteSpace(Search) ? null : Search, null, null, false, 1, 100);
            Services = new ObservableCollection<SocietyServiceDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load services ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
