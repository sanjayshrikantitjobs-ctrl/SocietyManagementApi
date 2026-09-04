using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Residents;

/// <summary>Mirrors members-list.component.ts — the legacy Member entity's
/// searchable list. (The newer Owner/Tenant occupancy model — owners-tab /
/// tenants-tab — is a separate, larger follow-up; this is the module the
/// web app itself still calls "Residents".)</summary>
public partial class ResidentsListViewModel : ObservableObject
{
    private readonly MembersClient _membersClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public ResidentsListViewModel(MembersClient membersClient, CurrentSocietyService currentSocietyService)
    {
        _membersClient = membersClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private ObservableCollection<MemberDto> members = new();
    [ObservableProperty] private string searchText = string.Empty;
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
            var response = await _membersClient.MembersGET3Async(
                societyId, string.IsNullOrWhiteSpace(SearchText) ? null : SearchText, null, null, 1, 50);
            Members = new ObservableCollection<MemberDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load residents ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
