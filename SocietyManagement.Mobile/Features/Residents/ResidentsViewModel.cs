using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Features.Residents.Forms;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Residents;

public record ResaleStatusOption(string Label, ListingStatus? Value);

/// <summary>Mirrors the web's Resident Management module (Overview/Owner/
/// Tenant/Vehicle Listing/Resale Listings tabs, all backed by the
/// FlatOccupancy/Vehicle/FlatResaleListing tables) — replaces the previous
/// legacy Member-list-only ResidentsListPage. Read-only for this pass: Add
/// Owner/Tenant/Vehicle/Listing and the NOC workflow are a follow-up, same
/// as every other module in this batch.</summary>
public partial class ResidentsViewModel : ObservableObject
{
    private static readonly ResaleStatusOption[] ResaleStatusOptionsSeed =
    {
        new("All Statuses", null),
        new("Available", Api.Generated.ListingStatus.Available),
        new("Under Negotiation", Api.Generated.ListingStatus.UnderNegotiation),
        new("Sold", Api.Generated.ListingStatus.Sold),
        new("Withdrawn", Api.Generated.ListingStatus.Withdrawn),
    };

    private readonly ResidentsOverviewClient _overviewClient;
    private readonly FlatOccupanciesClient _occupanciesClient;
    private readonly VehiclesClient _vehiclesClient;
    private readonly FlatResaleListingsClient _listingsClient;
    private readonly FlatsClient _flatsClient;
    private readonly CurrentSocietyService _currentSocietyService;
    private List<FlatDto> _flatsCache = new();

    public ResidentsViewModel(
        ResidentsOverviewClient overviewClient, FlatOccupanciesClient occupanciesClient,
        VehiclesClient vehiclesClient, FlatResaleListingsClient listingsClient, FlatsClient flatsClient,
        CurrentSocietyService currentSocietyService)
    {
        _overviewClient = overviewClient;
        _occupanciesClient = occupanciesClient;
        _vehiclesClient = vehiclesClient;
        _listingsClient = listingsClient;
        _flatsClient = flatsClient;
        _currentSocietyService = currentSocietyService;
    }

    private async Task<int?> ResolveFlatIdByNumberAsync(string flatNumber)
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return null;

        if (_flatsCache.Count == 0)
        {
            var flatsResponse = await _flatsClient.FlatsGETAsync(null, null, null, societyId, 1, 500);
            _flatsCache = (flatsResponse.Data?.Items ?? new()).ToList();
        }

        return _flatsCache.FirstOrDefault(f => string.Equals(f.FlatNumber, flatNumber, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    [ObservableProperty] private string selectedTab = "Overview";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<ResaleStatusOption> ResaleStatusOptions { get; } = new(ResaleStatusOptionsSeed);

    [RelayCommand]
    private async Task SelectTabAsync(string tab)
    {
        SelectedTab = tab;
        ErrorMessage = null;
        switch (tab)
        {
            case "Overview": await LoadOverviewAsync(); break;
            case "Owner": await LoadOwnersAsync(); break;
            case "Tenant": await LoadTenantsAsync(); break;
            case "Vehicles": await LoadVehiclesAsync(); break;
            case "Resale": await LoadResaleListingsAsync(); break;
        }
    }

    // ==================== Overview ====================

    [ObservableProperty] private ResidentsOverviewSummaryDto? summary;
    [ObservableProperty] private ObservableCollection<RecentOccupancyChangeDto> recentChanges = new();
    [ObservableProperty] private Chart? occupancyChart;

    [RelayCommand]
    private async Task LoadOverviewAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var summaryResponse = await _overviewClient.Summary2Async(societyId);
            Summary = summaryResponse.Data;

            if (Summary != null)
            {
                var ownerOccupied = Summary.OwnerOccupiedFlats ?? 0;
                var tenantOccupied = Summary.TenantOccupiedFlats ?? 0;
                var vacant = Summary.VacantFlats ?? 0;
                OccupancyChart = ChartFactory.BuildCategoryDonut(new List<(string?, double)>
                {
                    ("Owner Occupied", ownerOccupied), ("Tenant Occupied", tenantOccupied), ("Vacant", vacant)
                });
            }

            var changesResponse = await _overviewClient.RecentChangesAsync(societyId, 10);
            RecentChanges = new ObservableCollection<RecentOccupancyChangeDto>(changesResponse.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the residents overview ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenFlatDetailAsync(object? flat)
    {
        if (Shell.Current is null) return;

        var (flatId, flatNumber, occupancyType) = flat switch
        {
            FlatOwnershipGridDto o => (o.FlatId, o.FlatNumber, "Owner"),
            FlatTenancyGridDto t => (t.FlatId, t.FlatNumber, "Tenant"),
            _ => ((int?)null, (string?)null, (string?)null)
        };
        if (flatId is not int id || occupancyType is null) return;

        await Shell.Current.GoToAsync(nameof(FlatResidentDetailPage), new Dictionary<string, object>
        {
            ["flatId"] = id,
            ["flatNumber"] = flatNumber ?? string.Empty,
            ["occupancyType"] = occupancyType
        });
    }

    // ==================== Owner ====================

    [ObservableProperty] private ObservableCollection<FlatOwnershipGridDto> owners = new();
    [ObservableProperty] private string ownerSearch = string.Empty;

    [RelayCommand]
    private async Task LoadOwnersAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _occupanciesClient.OwnersGridAsync(
                societyId, string.IsNullOrWhiteSpace(OwnerSearch) ? null : OwnerSearch, null, false, 1, 100);
            Owners = new ObservableCollection<FlatOwnershipGridDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load owners ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Tenant ====================

    [ObservableProperty] private ObservableCollection<FlatTenancyGridDto> tenants = new();
    [ObservableProperty] private string tenantSearch = string.Empty;

    [RelayCommand]
    private async Task LoadTenantsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _occupanciesClient.TenantsGridAsync(
                societyId, string.IsNullOrWhiteSpace(TenantSearch) ? null : TenantSearch, null, false, 1, 100);
            Tenants = new ObservableCollection<FlatTenancyGridDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load tenants ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Vehicle Listing ====================

    [ObservableProperty] private ObservableCollection<VehicleDto> vehicles = new();
    [ObservableProperty] private string vehicleSearch = string.Empty;

    [RelayCommand]
    private async Task LoadVehiclesAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _vehiclesClient.VehiclesGETAsync(
                null, null, societyId, string.IsNullOrWhiteSpace(VehicleSearch) ? null : VehicleSearch, null, false, 1, 100);
            Vehicles = new ObservableCollection<VehicleDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load vehicles ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddVehicleAsync()
    {
        if (Shell.Current is null) return;

        var flatNumber = await Shell.Current.DisplayPromptAsync("Add Vehicle", "Flat number");
        if (string.IsNullOrWhiteSpace(flatNumber)) return;

        var flatId = await ResolveFlatIdByNumberAsync(flatNumber);
        if (flatId is null)
        {
            ErrorMessage = $"No flat numbered \"{flatNumber}\" was found in this society.";
            return;
        }

        await Shell.Current.GoToAsync(nameof(VehicleFormPage), new Dictionary<string, object> { ["flatId"] = flatId.Value });
    }

    [RelayCommand]
    private async Task EditVehicleAsync(VehicleDto vehicle)
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(VehicleFormPage), new Dictionary<string, object>
        {
            ["flatId"] = vehicle.FlatId ?? 0,
            ["vehicle"] = vehicle
        });
    }

    // ==================== Resale Listings ====================

    [ObservableProperty] private ObservableCollection<FlatResaleListingDto> resaleListings = new();
    [ObservableProperty] private ResaleStatusOption selectedResaleStatus = ResaleStatusOptionsSeed[0];

    partial void OnSelectedResaleStatusChanged(ResaleStatusOption value) => _ = LoadResaleListingsCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadResaleListingsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _listingsClient.FlatResaleListingsGETAsync(societyId, SelectedResaleStatus.Value, 1, 100);
            ResaleListings = new ObservableCollection<FlatResaleListingDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load resale listings ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddResaleListingAsync()
    {
        if (Shell.Current is null) return;

        var flatNumber = await Shell.Current.DisplayPromptAsync("Add Resale Listing", "Flat number");
        if (string.IsNullOrWhiteSpace(flatNumber)) return;

        var flatId = await ResolveFlatIdByNumberAsync(flatNumber);
        if (flatId is null)
        {
            ErrorMessage = $"No flat numbered \"{flatNumber}\" was found in this society.";
            return;
        }

        await Shell.Current.GoToAsync(nameof(ResaleListingFormPage), new Dictionary<string, object> { ["flatId"] = flatId.Value });
    }

    [RelayCommand]
    private async Task EditResaleListingAsync(FlatResaleListingDto listing)
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(ResaleListingFormPage), new Dictionary<string, object>
        {
            ["flatId"] = listing.FlatId ?? 0,
            ["listing"] = listing
        });
    }
}
