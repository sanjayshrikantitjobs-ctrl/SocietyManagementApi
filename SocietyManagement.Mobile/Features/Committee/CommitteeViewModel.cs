using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Committee;

/// <summary>Read-only pass mirroring the web's Committee directory
/// (Chairman/Secretary/Treasurer/etc.). Add/Edit/Delete are a follow-up.</summary>
public partial class CommitteeViewModel : ObservableObject
{
    private readonly CommitteeClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public CommitteeViewModel(CommitteeClient client, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private ObservableCollection<CommitteeMemberDto> members = new();
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
            var response = await _client.CommitteeGETAsync(societyId);
            Members = new ObservableCollection<CommitteeMemberDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the committee ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
