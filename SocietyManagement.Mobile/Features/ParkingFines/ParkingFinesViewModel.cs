using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.ParkingFines;

/// <summary>Mirrors parking-fines-list.component.ts + create-parking-fine-dialog.component.ts —
/// list of recorded fines, plus a form to record a new one. Vehicle
/// selection reuses the same search-then-select pattern as the New Visitor
/// flat picker, backed by the non-persisting vehicle-scans/search lookup
/// (the same endpoint the web app's OCR auto-match convenience uses).</summary>
public partial class ParkingFinesViewModel : ObservableObject
{
    private readonly ParkingFinesClient _finesClient;
    private readonly VehicleScansClient _scansClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public ParkingFinesViewModel(ParkingFinesClient finesClient, VehicleScansClient scansClient, CurrentSocietyService currentSocietyService)
    {
        _finesClient = finesClient;
        _scansClient = scansClient;
        _currentSocietyService = currentSocietyService;
        Reasons = new ObservableCollection<ParkingFineReason>(Enum.GetValues<ParkingFineReason>());
        FineDate = DateTime.Today;
    }

    [ObservableProperty] private ObservableCollection<ParkingFineDto> fines = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [ObservableProperty] private bool isAdding;

    [ObservableProperty] private string vehicleSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<VehicleSearchItemDto> vehicleSearchResults = new();
    [ObservableProperty] private VehicleSearchItemDto? selectedVehicle;

    public ObservableCollection<ParkingFineReason> Reasons { get; }
    [ObservableProperty] private ParkingFineReason selectedReason;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string amount = string.Empty;
    [ObservableProperty] private DateTime fineDate;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null)
        {
            ErrorMessage = "No society available for this account.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _finesClient.ParkingFinesGETAsync(societyId, null, null, 1, 50);
            Fines = new ObservableCollection<ParkingFineDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load parking fines ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void StartAdd()
    {
        VehicleSearchText = string.Empty;
        VehicleSearchResults = new ObservableCollection<VehicleSearchItemDto>();
        SelectedVehicle = null;
        SelectedReason = ParkingFineReason.NoParkingZone;
        Notes = string.Empty;
        Amount = string.Empty;
        FineDate = DateTime.Today;
        ErrorMessage = null;
        IsAdding = true;
    }

    [RelayCommand]
    private void CancelAdd() => IsAdding = false;

    [RelayCommand]
    private async Task SearchVehiclesAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        if (string.IsNullOrWhiteSpace(VehicleSearchText))
        {
            VehicleSearchResults = new ObservableCollection<VehicleSearchItemDto>();
            return;
        }

        try
        {
            var response = await _scansClient.Search2Async(societyId, VehicleSearchText);
            VehicleSearchResults = new ObservableCollection<VehicleSearchItemDto>(response.Data ?? new());
        }
        catch
        {
            // Same stance as the flat search — a transient miss just means no
            // results for this keystroke.
        }
    }

    [RelayCommand]
    private void SelectVehicle(VehicleSearchItemDto vehicle)
    {
        SelectedVehicle = vehicle;
        VehicleSearchResults = new ObservableCollection<VehicleSearchItemDto>();
        VehicleSearchText = vehicle.RegistrationNumber ?? string.Empty;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null)
        {
            ErrorMessage = "No society available for this account.";
            return;
        }

        ErrorMessage = null;
        if (SelectedVehicle?.VehicleId is not int vehicleId)
        {
            ErrorMessage = "Search for and select the vehicle.";
            return;
        }
        if (!double.TryParse(Amount, out var amountValue) || amountValue <= 0)
        {
            ErrorMessage = "Enter a valid fine amount.";
            return;
        }

        IsBusy = true;
        try
        {
            await _finesClient.ParkingFinesPOSTAsync(new CreateParkingFineCommand
            {
                SocietyId = societyId,
                VehicleId = vehicleId,
                ParkingSlotId = null,
                Reason = SelectedReason,
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                Amount = amountValue,
                FineDate = FineDate,
                PhotoBytes = null
            });

            IsAdding = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't record the fine ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
