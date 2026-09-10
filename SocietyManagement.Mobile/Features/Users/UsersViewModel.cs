using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Users;

/// <summary>Read-only pass mirroring the web's User Management list. Create/
/// Edit/Lock/Reset Password are a follow-up.</summary>
public partial class UsersViewModel : ObservableObject
{
    private readonly UsersClient _client;

    public UsersViewModel(UsersClient client) => _client = client;

    [ObservableProperty] private ObservableCollection<UserDto> users = new();
    [ObservableProperty] private string search = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.UsersGETAsync(
                string.IsNullOrWhiteSpace(Search) ? null : Search, null, null, null, false, 1, 100);
            Users = new ObservableCollection<UserDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load users ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
