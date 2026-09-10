using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Complaints;

/// <summary>Member self-service — every complaint the signed-in resident
/// has personally raised. Read-only for this pass; Raise Complaint is a
/// follow-up.</summary>
public partial class MyComplaintsViewModel : ObservableObject
{
    private readonly ComplaintsClient _client;

    public MyComplaintsViewModel(ComplaintsClient client) => _client = client;

    [ObservableProperty] private ObservableCollection<ComplaintDto> complaints = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.MineAsync();
            Complaints = new ObservableCollection<ComplaintDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load your complaints ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
