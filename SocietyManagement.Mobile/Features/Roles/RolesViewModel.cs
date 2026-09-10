using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Roles;

/// <summary>Read-only pass mirroring the web's Roles list (Name/Description/
/// User Count). The permission-matrix editor is a follow-up.</summary>
public partial class RolesViewModel : ObservableObject
{
    private readonly RolesClient _client;

    public RolesViewModel(RolesClient client) => _client = client;

    [ObservableProperty] private ObservableCollection<RoleDto> roles = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.RolesGETAsync();
            Roles = new ObservableCollection<RoleDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load roles ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
