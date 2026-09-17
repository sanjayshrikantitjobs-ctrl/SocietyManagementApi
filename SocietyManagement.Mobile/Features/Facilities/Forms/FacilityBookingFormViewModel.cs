using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Features.Facilities.Forms;

/// <summary>Mirrors facility-booking-form-dialog.component.ts — a pure
/// request form; the actual charge is always computed and validated
/// server-side (see FacilityBookingFeature.CreateFacilityBookingCommandHandler).</summary>
public partial class FacilityBookingFormViewModel : ObservableObject
{
    private readonly FacilityBookingsClient _client;
    private readonly FlatsClient _flatsClient;
    private readonly AuthState _auth;

    public FacilityBookingFormViewModel(FacilityBookingsClient client, FlatsClient flatsClient, AuthState auth)
    {
        _client = client;
        _flatsClient = flatsClient;
        _auth = auth;
        BookingDate = DateTime.Today;
        StartTime = new TimeSpan(10, 0, 0);
        EndTime = new TimeSpan(13, 0, 0);
    }

    [ObservableProperty] private FacilityDto? facility;
    [ObservableProperty] private ObservableCollection<FlatDto> flats = new();
    [ObservableProperty] private FlatDto? selectedFlat;
    [ObservableProperty] private DateTime bookingDate;
    [ObservableProperty] private TimeSpan startTime;
    [ObservableProperty] private TimeSpan endTime;
    [ObservableProperty] private string purpose = string.Empty;
    [ObservableProperty] private int guestCount;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool CanManage => _auth.IsAdmin;

    public void LoadFacility(FacilityDto facilityDto, DateTime bookingDate)
    {
        Facility = facilityDto;
        BookingDate = bookingDate;
    }

    // Admin/SuperAdmin can book on behalf of any flat in the society — the
    // backend already allows this (see CreateFacilityBookingCommandHandler);
    // a resident only ever sees their own flat(s).
    public async Task LoadFlatsAsync()
    {
        try
        {
            IEnumerable<FlatDto>? flats;
            if (CanManage)
            {
                var response = await _flatsClient.FlatsGETAsync(null, null, null, Facility?.SocietyId, 1, 500);
                flats = response.Data?.Items;
            }
            else
            {
                var response = await _flatsClient.Mine5Async();
                flats = response.Data;
            }

            Flats = new ObservableCollection<FlatDto>(flats ?? new List<FlatDto>());
            if (Flats.Count > 0) SelectedFlat = Flats[0];
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load flats ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Facility?.Id is not int facilityId || SelectedFlat?.Id is not int flatId)
        {
            ErrorMessage = "Select a flat before booking.";
            return;
        }
        if (EndTime <= StartTime)
        {
            ErrorMessage = "End time must be after start time.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _client.FacilityBookingsPOSTAsync(new CreateFacilityBookingCommand
            {
                FacilityId = facilityId, FlatId = flatId, BookingDate = new DateTimeOffset(BookingDate.Date, TimeSpan.Zero),
                StartTime = StartTime.ToString(@"hh\:mm\:ss"), EndTime = EndTime.ToString(@"hh\:mm\:ss"),
                Purpose = string.IsNullOrWhiteSpace(Purpose) ? null : Purpose, GuestCount = GuestCount,
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes
            });

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't complete the booking ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
