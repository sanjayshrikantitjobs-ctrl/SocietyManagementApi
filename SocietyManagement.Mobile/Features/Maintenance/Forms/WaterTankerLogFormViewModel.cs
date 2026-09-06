using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Maintenance.Forms;

/// <summary>Mirrors the web's Water Tanker "Log Tanker Entry"/"Edit Tanker
/// Entry" dialog — TotalAmount is a computed field (NumberOfTankers ×
/// PricePerTanker), never part of the create/update payload.</summary>
public partial class WaterTankerLogFormViewModel : ObservableObject
{
    private readonly WaterTankerLogsClient _client;

    public WaterTankerLogFormViewModel(WaterTankerLogsClient client)
    {
        _client = client;
    }

    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private DateTime date = DateTime.Today;
    [ObservableProperty] private string providerName = string.Empty;
    [ObservableProperty] private string vehicleNumber = string.Empty;
    [ObservableProperty] private int numberOfTankers = 1;
    [ObservableProperty] private decimal pricePerTanker;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    public void LoadFrom(WaterTankerLogDto log)
    {
        Id = log.Id ?? 0;
        if (log.Date is DateTimeOffset date) Date = date.Date;
        ProviderName = log.ProviderName ?? string.Empty;
        VehicleNumber = log.VehicleNumber ?? string.Empty;
        NumberOfTankers = log.NumberOfTankers ?? 1;
        PricePerTanker = (decimal)(log.PricePerTanker ?? 0);
        Notes = log.Notes;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(ProviderName) || string.IsNullOrWhiteSpace(VehicleNumber) || NumberOfTankers <= 0)
        {
            ErrorMessage = "Enter a provider name, vehicle number, and at least 1 tanker.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.WaterTankerLogsPUTAsync(Id, new UpdateWaterTankerLogCommand
                {
                    Id = Id, Date = Date, ProviderName = ProviderName, VehicleNumber = VehicleNumber,
                    NumberOfTankers = NumberOfTankers, PricePerTanker = (double)PricePerTanker, Notes = Notes
                });
            }
            else
            {
                await _client.WaterTankerLogsPOSTAsync(new CreateWaterTankerLogCommand
                {
                    SocietyId = SocietyId, Date = Date, ProviderName = ProviderName, VehicleNumber = VehicleNumber,
                    NumberOfTankers = NumberOfTankers, PricePerTanker = (double)PricePerTanker, Notes = Notes
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the entry ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
