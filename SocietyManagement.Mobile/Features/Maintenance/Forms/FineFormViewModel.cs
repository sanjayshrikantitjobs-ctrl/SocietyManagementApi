using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Maintenance.Forms;

/// <summary>Mirrors the web's "Record Fine" dialog — fines have no Edit
/// action at all (only create/waive/delete), so this page is create-only.</summary>
public partial class FineFormViewModel : ObservableObject
{
    private readonly FineRecordsClient _client;
    private readonly FlatsClient _flatsClient;

    public FineFormViewModel(FineRecordsClient client, FlatsClient flatsClient)
    {
        _client = client;
        _flatsClient = flatsClient;
    }

    [ObservableProperty] private int societyId;
    [ObservableProperty] private string flatSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<FlatDto> flatSearchResults = new();
    [ObservableProperty] private FlatDto? selectedFlat;
    [ObservableProperty] private string reason = string.Empty;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime fineDate = DateTime.Today;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    partial void OnFlatSearchTextChanged(string value) => _ = SearchFlatsCommand.ExecuteAsync(null);

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
        if (SelectedFlat is null || string.IsNullOrWhiteSpace(Reason) || Amount <= 0)
        {
            ErrorMessage = "Select a flat, enter a reason, and an amount greater than zero.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _client.FineRecordsPOSTAsync(new CreateFineRecordCommand
            {
                FlatId = SelectedFlat.Id, Reason = Reason, Amount = (double)Amount, FineDate = FineDate
            });
            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
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
