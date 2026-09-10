using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Residents;

/// <summary>Member self-service — every active member of the signed-in
/// resident's own current flat occupancy. Read-only for this pass; Add/
/// Remove Family Member are a follow-up.</summary>
public partial class MyFamilyViewModel : ObservableObject
{
    private readonly FlatOccupanciesClient _client;

    public MyFamilyViewModel(FlatOccupanciesClient client) => _client = client;

    [ObservableProperty] private ObservableCollection<OccupancyMemberDto> members = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.MembersGET3Async();
            Members = new ObservableCollection<OccupancyMemberDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load your family members ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
