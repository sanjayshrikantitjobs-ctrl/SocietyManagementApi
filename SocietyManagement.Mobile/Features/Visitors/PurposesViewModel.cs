using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Visitors.Forms;

namespace SocietyManagement.Mobile.Features.Visitors;

/// <summary>Mirrors purposes.component.ts — same Admin-only-mutation, Auth.IsAdmin gating as GatesViewModel.</summary>
public partial class PurposesViewModel : ObservableObject
{
    private readonly VisitorPurposesClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public PurposesViewModel(VisitorPurposesClient client, CurrentSocietyService currentSocietyService, AuthState authState)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
        Auth = authState;
    }

    public AuthState Auth { get; }

    [ObservableProperty] private ObservableCollection<VisitorPurposeDto> purposes = new();
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
            var response = await _client.VisitorPurposesGETAsync(societyId, null);
            Purposes = new ObservableCollection<VisitorPurposeDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load purposes ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddPurposeAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        await Shell.Current.GoToAsync(nameof(PurposeFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowPurposeActionsAsync(VisitorPurposeDto purpose)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(purpose.Name, "Cancel", null, "Edit", "Delete");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(PurposeFormPage), new Dictionary<string, object> { ["purpose"] = purpose });
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Purpose", $"Delete \"{purpose.Name}\"?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _client.VisitorPurposesDELETEAsync(purpose.Id ?? 0);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the purpose ({ex.Message}).";
                }
                break;
        }
    }
}
