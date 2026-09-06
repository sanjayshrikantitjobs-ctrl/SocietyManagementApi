using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Maintenance.Forms;

public record ChargeFrequencyOption(string Label, ChargeFrequency Value);

/// <summary>Mirrors the web's Special Charges Add/Edit dialog. Flat is a
/// search-as-you-type picker (same adaptation NewVisitorViewModel already
/// uses) instead of the web's up-front-loaded dropdown, and is locked once
/// editing (UpdateSpecialChargeCommand has no FlatId field).</summary>
public partial class SpecialChargeFormViewModel : ObservableObject
{
    private readonly SpecialChargesClient _client;
    private readonly FlatsClient _flatsClient;

    public SpecialChargeFormViewModel(SpecialChargesClient client, FlatsClient flatsClient)
    {
        _client = client;
        _flatsClient = flatsClient;
        selectedFrequency = FrequencyOptions[0];
    }

    public List<ChargeFrequencyOption> FrequencyOptions { get; } = new()
    {
        new("Monthly", ChargeFrequency.Monthly), new("One-time", ChargeFrequency.OneTime),
    };

    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string flatSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<FlatDto> flatSearchResults = new();
    [ObservableProperty] private FlatDto? selectedFlat;
    [ObservableProperty] private string? existingFlatNumber;
    [ObservableProperty] private string chargeName = string.Empty;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private ChargeFrequencyOption selectedFrequency;
    [ObservableProperty] private DateTime startDate = DateTime.Today;
    [ObservableProperty] private DateTime endDate = DateTime.Today.AddMonths(1);
    [ObservableProperty] private bool hasEndDate;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    private bool _existingIsActive = true;

    public bool IsEditMode => Id > 0;

    partial void OnFlatSearchTextChanged(string value) => _ = SearchFlatsCommand.ExecuteAsync(null);
    partial void OnEndDateChanged(DateTime value) => HasEndDate = true;

    public void LoadFrom(SpecialChargeDto charge)
    {
        Id = charge.Id ?? 0;
        ExistingFlatNumber = charge.FlatNumber;
        ChargeName = charge.ChargeName ?? string.Empty;
        Amount = (decimal)(charge.Amount ?? 0);
        SelectedFrequency = FrequencyOptions.FirstOrDefault(o => o.Value == charge.Frequency) ?? FrequencyOptions[0];
        if (charge.StartDate is DateTimeOffset start) StartDate = start.Date;
        if (charge.EndDate is DateTimeOffset end) { EndDate = end.Date; HasEndDate = true; }
        Notes = charge.Notes;
        _existingIsActive = charge.IsActive ?? true;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SearchFlatsAsync()
    {
        if (string.IsNullOrWhiteSpace(FlatSearchText) || SocietyId <= 0)
        {
            FlatSearchResults = new ObservableCollection<FlatDto>();
            return;
        }
        try
        {
            var response = await _flatsClient.FlatsGETAsync(null, null, FlatSearchText, SocietyId, 1, 20);
            FlatSearchResults = new ObservableCollection<FlatDto>(response.Data?.Items ?? new());
        }
        catch
        {
            // Search-as-you-type — a transient failure just means no results shown.
        }
    }

    [RelayCommand]
    private void SelectFlat(FlatDto flat)
    {
        SelectedFlat = flat;
        FlatSearchResults = new ObservableCollection<FlatDto>();
        FlatSearchText = flat.FlatNumber ?? string.Empty;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (!IsEditMode && SelectedFlat is null)
        {
            ErrorMessage = "Search and select a flat.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ChargeName) || Amount <= 0)
        {
            ErrorMessage = "Enter a charge name and an amount greater than zero.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var endDate = HasEndDate ? (DateTime?)EndDate : null;
            if (IsEditMode)
            {
                await _client.SpecialChargesPUTAsync(Id, new UpdateSpecialChargeCommand
                {
                    Id = Id, ChargeName = ChargeName, Amount = (double)Amount, Frequency = SelectedFrequency.Value,
                    StartDate = StartDate, EndDate = endDate, Notes = Notes, IsActive = _existingIsActive
                });
            }
            else
            {
                await _client.SpecialChargesPOSTAsync(new CreateSpecialChargeCommand
                {
                    FlatId = SelectedFlat!.Id, ChargeName = ChargeName, Amount = (double)Amount, Frequency = SelectedFrequency.Value,
                    StartDate = StartDate, EndDate = endDate, Notes = Notes
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the special charge ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
