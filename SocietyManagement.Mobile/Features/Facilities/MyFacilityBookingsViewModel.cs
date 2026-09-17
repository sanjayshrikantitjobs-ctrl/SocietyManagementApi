using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Facilities;

public partial class MyFacilityBookingsViewModel : ObservableObject
{
    private readonly FacilityBookingsClient _client;

    public MyFacilityBookingsViewModel(FacilityBookingsClient client) => _client = client;

    [ObservableProperty] private ObservableCollection<FacilityBookingDto> bookings = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.Mine4Async(null, 1, 100);
            Bookings = new ObservableCollection<FacilityBookingDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load your bookings ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync(FacilityBookingDto booking)
    {
        if (booking.Id is not int id || Shell.Current is null) return;

        var confirmed = await Shell.Current.DisplayAlert("Cancel Booking", $"Cancel your booking for \"{booking.FacilityName}\"?", "Yes", "No");
        if (!confirmed) return;

        try
        {
            await _client.Cancel4Async(id, null);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't cancel the booking ({ex.Message}).";
        }
    }
}
