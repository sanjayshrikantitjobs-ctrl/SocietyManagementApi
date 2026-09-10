using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Residents.Forms;

/// <summary>Add/Edit a vehicle — shared by the flat detail page's Vehicles
/// card (FlatId already known) and the top-level Vehicle Listing tab
/// (ResidentsViewModel resolves a flat number to a FlatId first, same
/// lookup-by-number pattern ParkingSlotsViewModel's Allocate uses).</summary>
public partial class VehicleFormViewModel : ObservableObject
{
    private readonly VehiclesClient _client;

    public VehicleFormViewModel(VehiclesClient client)
    {
        _client = client;
        selectedType = TypeOptions[0];
    }

    public List<VehicleType> TypeOptions { get; } = new() { VehicleType.FourWheeler, VehicleType.TwoWheeler };

    [ObservableProperty] private int id;
    [ObservableProperty] private int flatId;
    [ObservableProperty] private VehicleType selectedType;
    [ObservableProperty] private string registrationNumber = string.Empty;
    [ObservableProperty] private string? make;
    [ObservableProperty] private string? model;
    [ObservableProperty] private string? color;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    /// <summary>No GetById endpoint exists for vehicles (only per-flat/
    /// per-society lists) — the row tapped is passed straight through.</summary>
    public void LoadFrom(VehicleDto vehicle)
    {
        Id = vehicle.Id ?? 0;
        FlatId = vehicle.FlatId ?? 0;
        SelectedType = vehicle.VehicleType ?? TypeOptions[0];
        RegistrationNumber = vehicle.RegistrationNumber ?? string.Empty;
        Make = vehicle.Make;
        Model = vehicle.Model;
        Color = vehicle.Color;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            ErrorMessage = "Registration number is required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.VehiclesPUTAsync(Id, new UpdateVehicleCommand
                {
                    Id = Id, FlatId = FlatId, VehicleType = SelectedType, RegistrationNumber = RegistrationNumber,
                    Make = Make, Model = Model, Color = Color
                });
            }
            else
            {
                await _client.VehiclesPOSTAsync(new CreateVehicleCommand
                {
                    FlatId = FlatId, VehicleType = SelectedType, RegistrationNumber = RegistrationNumber,
                    Make = Make, Model = Model, Color = Color
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save this vehicle ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
