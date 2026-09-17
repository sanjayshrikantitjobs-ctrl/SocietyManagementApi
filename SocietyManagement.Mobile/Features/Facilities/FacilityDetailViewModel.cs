using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Features.Facilities.Forms;

namespace SocietyManagement.Mobile.Features.Facilities;

/// <summary>Mirrors facility-detail.component.ts: facility info, a date
/// picker driving the availability list for that day, and a Book action —
/// the mobile equivalent of the web's "Facility Details/Availability" +
/// "Book Facility" pages combined into one screen.</summary>
public partial class FacilityDetailViewModel : ObservableObject
{
    private readonly FacilitiesClient _client;

    public FacilityDetailViewModel(FacilitiesClient client)
    {
        _client = client;
        SelectedDate = DateTime.Today;
    }

    [ObservableProperty] private int facilityId;
    [ObservableProperty] private FacilityDto? facility;
    [ObservableProperty] private DateTime selectedDate;
    [ObservableProperty] private ObservableCollection<FacilitySlotDto> slots = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    async partial void OnFacilityIdChanged(int value) => await LoadAsync();
    async partial void OnSelectedDateChanged(DateTime value) => await LoadAvailabilityAsync();

    private async Task LoadAsync()
    {
        if (FacilityId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.FacilitiesGET2Async(FacilityId);
            Facility = response.Data;
            await LoadAvailabilityAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the facility ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAvailabilityAsync()
    {
        if (FacilityId <= 0) return;
        try
        {
            var response = await _client.Availability2Async(FacilityId, SelectedDate);
            Slots = new ObservableCollection<FacilitySlotDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load availability ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task BookAsync()
    {
        if (Facility is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(FacilityBookingFormPage),
            new Dictionary<string, object> { ["facility"] = Facility, ["bookingDate"] = SelectedDate });
    }
}
