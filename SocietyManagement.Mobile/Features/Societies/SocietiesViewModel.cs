using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Societies;

/// <summary>Super Admin only — every society on the platform, plus the same
/// three row actions the web offers per society (Society Structure,
/// Parking Slots, and an Edit/Delete menu) — see SocietiesController's own
/// endpoints for what each opens.</summary>
public partial class SocietiesViewModel : ObservableObject
{
    private readonly SocietiesClient _client;

    public SocietiesViewModel(SocietiesClient client) => _client = client;

    [ObservableProperty] private ObservableCollection<SocietyDto> societies = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.SocietiesGETAsync();
            Societies = new ObservableCollection<SocietyDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load societies ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddSocietyAsync()
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(SocietyFormPage));
    }

    [RelayCommand]
    private async Task OpenStructureAsync(SocietyDto society)
    {
        if (Shell.Current is null || society.Id is not int id) return;
        await Shell.Current.GoToAsync(nameof(SocietyStructurePage), new Dictionary<string, object> { ["societyId"] = id });
    }

    [RelayCommand]
    private async Task OpenParkingAsync(SocietyDto society)
    {
        if (Shell.Current is null || society.Id is not int id) return;
        await Shell.Current.GoToAsync(nameof(ParkingSlotsPage), new Dictionary<string, object> { ["societyId"] = id });
    }

    [RelayCommand]
    private async Task ShowSocietyActionsAsync(SocietyDto society)
    {
        if (Shell.Current is null || society.Id is not int id) return;

        var action = await Shell.Current.DisplayActionSheet(society.Name, "Cancel", null, "Edit", "Delete");
        switch (action)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(SocietyFormPage), new Dictionary<string, object> { ["societyId"] = id });
                break;
            case "Delete":
                if (!await Shell.Current.DisplayAlert("Delete Society", $"Delete \"{society.Name}\" and everything under it? This cannot be undone.", "Delete", "Cancel")) return;
                try
                {
                    await _client.SocietiesDELETEAsync(id);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete this society ({ex.Message}).";
                }
                break;
        }
    }
}
