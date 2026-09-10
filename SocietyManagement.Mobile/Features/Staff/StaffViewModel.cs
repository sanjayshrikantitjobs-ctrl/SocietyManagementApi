using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Staff;

/// <summary>Read-only pass mirroring the web's Staff list (Name/Category/
/// Phone/Joining Date/Status). Add/Edit/Delete are a follow-up.</summary>
public partial class StaffViewModel : ObservableObject
{
    private readonly StaffClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public StaffViewModel(StaffClient client, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private ObservableCollection<StaffDto> staff = new();
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
            var response = await _client.StaffGETAsync(
                societyId, string.IsNullOrWhiteSpace(Search) ? null : Search, null, null, null, false, 1, 100);
            Staff = new ObservableCollection<StaffDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load staff ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
