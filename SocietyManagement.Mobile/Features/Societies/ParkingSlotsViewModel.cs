using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Societies;

/// <summary>Mirrors the web's Parking Slots screen (Slot No./Type/Status/
/// Allocated To + Add Slot/Allocate/Delete). Allocate asks for a flat
/// number rather than a dedicated flat picker — this society's flats are
/// fetched once and matched by number, keeping the flow to one native
/// prompt instead of a new picker page.</summary>
public partial class ParkingSlotsViewModel : ObservableObject
{
    private readonly ParkingSlotsClient _slotsClient;
    private readonly FlatsClient _flatsClient;
    private List<FlatDto> _flatsCache = new();

    public ParkingSlotsViewModel(ParkingSlotsClient slotsClient, FlatsClient flatsClient)
    {
        _slotsClient = slotsClient;
        _flatsClient = flatsClient;
    }

    [ObservableProperty] private int societyId;
    [ObservableProperty] private ObservableCollection<ParkingSlotDto> slots = new();
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
            var response = await _slotsClient.ParkingSlotsGETAsync(SocietyId, null);
            Slots = new ObservableCollection<ParkingSlotDto>(response.Data ?? new());

            var flatsResponse = await _flatsClient.FlatsGETAsync(null, null, null, SocietyId, 1, 500);
            _flatsCache = (flatsResponse.Data?.Items ?? new()).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load parking slots ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddSlotAsync()
    {
        if (Shell.Current is null) return;

        var slotNumber = await Shell.Current.DisplayPromptAsync("Add Parking Slot", "Slot number");
        if (string.IsNullOrWhiteSpace(slotNumber)) return;

        var typeChoice = await Shell.Current.DisplayActionSheet("Slot Type", "Cancel", null, "Four Wheeler", "Two Wheeler", "Visitor");
        var type = typeChoice switch
        {
            "Four Wheeler" => ParkingType.FourWheeler,
            "Two Wheeler" => ParkingType.TwoWheeler,
            "Visitor" => ParkingType.Visitor,
            _ => (ParkingType?)null
        };
        if (type is null) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _slotsClient.ParkingSlotsPOSTAsync(new CreateParkingSlotCommand { SocietyId = SocietyId, SlotNumber = slotNumber, Type = type });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't add the slot ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AllocateAsync(ParkingSlotDto slot)
    {
        if (Shell.Current is null || slot.Id is not int id) return;

        if (slot.Status == ParkingStatus.Allocated)
        {
            var confirmed = await Shell.Current.DisplayAlert("Vacate Slot", $"Vacate slot {slot.SlotNumber}?", "Vacate", "Cancel");
            if (!confirmed) return;
            await AllocateToFlatAsync(id, null);
            return;
        }

        var flatNumber = await Shell.Current.DisplayPromptAsync("Allocate Slot", $"Flat number for slot {slot.SlotNumber}");
        if (string.IsNullOrWhiteSpace(flatNumber)) return;

        var flat = _flatsCache.FirstOrDefault(f => string.Equals(f.FlatNumber, flatNumber, StringComparison.OrdinalIgnoreCase));
        if (flat?.Id is not int flatId)
        {
            ErrorMessage = $"No flat numbered \"{flatNumber}\" was found in this society.";
            return;
        }

        await AllocateToFlatAsync(id, flatId);
    }

    private async Task AllocateToFlatAsync(int slotId, int? flatId)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _slotsClient.AllocateAsync(slotId, flatId);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update this slot's allocation ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSlotAsync(ParkingSlotDto slot)
    {
        if (Shell.Current is null || slot.Id is not int id) return;

        var confirmed = await Shell.Current.DisplayAlert("Delete Slot", $"Delete slot {slot.SlotNumber}?", "Delete", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _slotsClient.ParkingSlotsDELETEAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete this slot ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
