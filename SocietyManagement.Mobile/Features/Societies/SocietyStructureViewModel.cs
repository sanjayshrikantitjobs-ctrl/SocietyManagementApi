using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Societies;

/// <summary>One row of the Society Structure tree — wraps a BuildingDto with
/// the expand/collapse state and lazily-loaded Wings the raw DTO doesn't
/// carry, mirroring the web's own expandable building panel.</summary>
public partial class BuildingItemViewModel : ObservableObject
{
    public BuildingItemViewModel(BuildingDto dto)
    {
        Id = dto.Id ?? 0;
        Name = dto.Name ?? "—";
        WingCount = dto.WingCount ?? 0;
    }

    public int Id { get; }
    public string Name { get; }
    public int WingCount { get; }

    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private bool isLoadingWings;
    [ObservableProperty] private ObservableCollection<WingDto> wings = new();
}

/// <summary>Mirrors the web's Society Structure screen (Buildings expand to
/// their Wings, each Wing showing its floor count) — read-plus-quick-add
/// for this pass: Add Building/Add Wing use a native single-field prompt
/// rather than a dedicated form page, matching how little the web dialog
/// itself asks for (just a name). Floors aren't independently addable from
/// here (the web screenshot shows only a count, no add action), so this
/// stops one level short of Floors.</summary>
public partial class SocietyStructureViewModel : ObservableObject
{
    private readonly BuildingsClient _buildingsClient;
    private readonly WingsClient _wingsClient;

    public SocietyStructureViewModel(BuildingsClient buildingsClient, WingsClient wingsClient)
    {
        _buildingsClient = buildingsClient;
        _wingsClient = wingsClient;
    }

    [ObservableProperty] private int societyId;
    [ObservableProperty] private ObservableCollection<BuildingItemViewModel> buildings = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    async partial void OnSocietyIdChanged(int value)
    {
        if (value > 0) await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (SocietyId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _buildingsClient.BuildingsGETAsync(SocietyId);
            Buildings = new ObservableCollection<BuildingItemViewModel>((response.Data ?? new()).Select(b => new BuildingItemViewModel(b)));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the society structure ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleBuildingAsync(BuildingItemViewModel building)
    {
        building.IsExpanded = !building.IsExpanded;
        if (!building.IsExpanded || building.Wings.Count > 0) return;
        await LoadWingsAsync(building);
    }

    private async Task LoadWingsAsync(BuildingItemViewModel building)
    {
        building.IsLoadingWings = true;
        try
        {
            var response = await _wingsClient.WingsGETAsync(building.Id);
            building.Wings = new ObservableCollection<WingDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load wings for {building.Name} ({ex.Message}).";
        }
        finally
        {
            building.IsLoadingWings = false;
        }
    }

    [RelayCommand]
    private async Task AddBuildingAsync()
    {
        if (Shell.Current is null) return;
        var name = await Shell.Current.DisplayPromptAsync("Add Building", "Building name", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _buildingsClient.BuildingsPOSTAsync(new CreateBuildingCommand { SocietyId = SocietyId, Name = name });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't add the building ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddWingAsync(BuildingItemViewModel building)
    {
        if (Shell.Current is null) return;
        var name = await Shell.Current.DisplayPromptAsync("Add Wing", $"Wing name for {building.Name}", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _wingsClient.WingsPOSTAsync(new CreateWingCommand { BuildingId = building.Id, Name = name });
            building.IsExpanded = true;
            building.Wings.Clear();
            await LoadWingsAsync(building);
            // Not calling LoadAsync() here — it rebuilds every
            // BuildingItemViewModel from scratch, which would collapse this
            // one right back and discard the wings just loaded above. The
            // header's "N wing(s)" count goes stale until the next full
            // reload, which is an acceptable tradeoff.
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't add the wing ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteBuildingAsync(BuildingItemViewModel building)
    {
        if (Shell.Current is null) return;
        var confirmed = await Shell.Current.DisplayAlert(
            "Delete Building", $"Delete \"{building.Name}\" and everything under it?", "Delete", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _buildingsClient.BuildingsDELETEAsync(building.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete this building ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
