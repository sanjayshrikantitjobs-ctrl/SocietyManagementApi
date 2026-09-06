using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Visitors.Forms;

namespace SocietyManagement.Mobile.Features.Visitors;

/// <summary>Mirrors gates.component.ts — Admin-only management, but read
/// access (list) is shown to any role that can see the Visitors tab;
/// Add/Edit/Delete are gated by Auth.IsAdmin the same way Dashboard's
/// tiles gate admin-only modules.</summary>
public partial class GatesViewModel : ObservableObject
{
    private readonly GatesClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public GatesViewModel(GatesClient client, CurrentSocietyService currentSocietyService, AuthState authState)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
        Auth = authState;
    }

    public AuthState Auth { get; }

    [ObservableProperty] private ObservableCollection<GateDto> gates = new();
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
            var response = await _client.GatesGETAsync(societyId, null);
            Gates = new ObservableCollection<GateDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load gates ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddGateAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        await Shell.Current.GoToAsync(nameof(GateFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowGateActionsAsync(GateDto gate)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(gate.Name, "Cancel", null, "Edit", "Delete");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(GateFormPage), new Dictionary<string, object> { ["gate"] = gate });
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Gate", $"Delete \"{gate.Name}\"?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _client.GatesDELETEAsync(gate.Id ?? 0);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the gate ({ex.Message}).";
                }
                break;
        }
    }
}
