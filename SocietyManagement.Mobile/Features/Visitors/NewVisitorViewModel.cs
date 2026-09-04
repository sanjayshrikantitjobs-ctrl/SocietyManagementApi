using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Visitors;

/// <summary>Mirrors new-visitor.component.ts's capture flow: pick the
/// target flat, purpose and gate, enter the visitor's details, submit.
/// Flat selection is a search-as-you-type list here instead of the web's
/// Building/Wing/Floor cascade picker — a mobile-appropriate adaptation of
/// the same "pick one flat" step, not a different feature.</summary>
public partial class NewVisitorViewModel : ObservableObject
{
    private readonly FlatsClient _flatsClient;
    private readonly VisitorPurposesClient _purposesClient;
    private readonly GatesClient _gatesClient;
    private readonly VisitorVisitsClient _visitsClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public NewVisitorViewModel(
        FlatsClient flatsClient, VisitorPurposesClient purposesClient, GatesClient gatesClient,
        VisitorVisitsClient visitsClient, CurrentSocietyService currentSocietyService)
    {
        _flatsClient = flatsClient;
        _purposesClient = purposesClient;
        _gatesClient = gatesClient;
        _visitsClient = visitsClient;
        _currentSocietyService = currentSocietyService;
        NumberOfVisitors = 1;
    }

    [ObservableProperty] private string flatSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<FlatDto> flatSearchResults = new();
    [ObservableProperty] private FlatDto? selectedFlat;

    [ObservableProperty] private ObservableCollection<VisitorPurposeDto> purposes = new();
    [ObservableProperty] private VisitorPurposeDto? selectedPurpose;

    [ObservableProperty] private ObservableCollection<GateDto> gates = new();
    [ObservableProperty] private GateDto? selectedGate;

    [ObservableProperty] private string visitorName = string.Empty;
    [ObservableProperty] private string visitorMobile = string.Empty;
    [ObservableProperty] private string visitorVehicleNumber = string.Empty;
    [ObservableProperty] private int numberOfVisitors;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSearchingFlats;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? successMessage;

    public async Task LoadLookupsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null)
        {
            ErrorMessage = "No society available for this account.";
            return;
        }

        IsBusy = true;
        try
        {
            var purposesResponse = await _purposesClient.VisitorPurposesGETAsync(societyId, true);
            Purposes = new ObservableCollection<VisitorPurposeDto>(purposesResponse.Data ?? new());

            var gatesResponse = await _gatesClient.GatesGETAsync(societyId, true);
            Gates = new ObservableCollection<GateDto>(gatesResponse.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load purposes/gates ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchFlatsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        if (string.IsNullOrWhiteSpace(FlatSearchText))
        {
            FlatSearchResults = new ObservableCollection<FlatDto>();
            return;
        }

        IsSearchingFlats = true;
        try
        {
            var response = await _flatsClient.FlatsGETAsync(null, null, FlatSearchText, societyId, 1, 20);
            FlatSearchResults = new ObservableCollection<FlatDto>(response.Data?.Items ?? new());
        }
        catch
        {
            // Search-as-you-type — a transient failure just means no results
            // this keystroke; the user can keep typing or retry.
        }
        finally
        {
            IsSearchingFlats = false;
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
        ErrorMessage = null;

        if (SelectedFlat?.Id is not int flatId)
        {
            ErrorMessage = "Select the flat being visited.";
            return;
        }
        if (SelectedPurpose?.Id is not int purposeId)
        {
            ErrorMessage = "Select a purpose of visit.";
            return;
        }
        if (SelectedGate?.Id is not int gateId)
        {
            ErrorMessage = "Select the gate.";
            return;
        }
        if (string.IsNullOrWhiteSpace(VisitorName) || string.IsNullOrWhiteSpace(VisitorMobile))
        {
            ErrorMessage = "Enter the visitor's name and mobile number.";
            return;
        }

        IsBusy = true;
        try
        {
            await _visitsClient.VisitorVisitsPOSTAsync(new CreateVisitCommand
            {
                VisitorId = null,
                NewVisitorName = VisitorName.Trim(),
                NewVisitorMobile = VisitorMobile.Trim(),
                NewVisitorPhotoUrl = null,
                NewVisitorVehicleNumber = string.IsNullOrWhiteSpace(VisitorVehicleNumber) ? null : VisitorVehicleNumber.Trim(),
                NewVisitorVehicleType = null,
                FlatId = flatId,
                PurposeId = purposeId,
                GateId = gateId,
                NumberOfVisitors = NumberOfVisitors < 1 ? 1 : NumberOfVisitors
            });

            SuccessMessage = "Visitor request created.";
            var keptGate = SelectedGate;
            Reset();
            SelectedGate = keptGate; // same watchman, same gate — the next entry almost always repeats it
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't submit the visitor request ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Reset()
    {
        FlatSearchText = string.Empty;
        FlatSearchResults = new ObservableCollection<FlatDto>();
        SelectedFlat = null;
        SelectedPurpose = null;
        SelectedGate = null;
        VisitorName = string.Empty;
        VisitorMobile = string.Empty;
        VisitorVehicleNumber = string.Empty;
        NumberOfVisitors = 1;
        ErrorMessage = null;
        SuccessMessage = null;
    }
}
