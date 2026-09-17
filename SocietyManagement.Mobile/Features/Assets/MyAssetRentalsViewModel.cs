using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Assets;

public partial class MyAssetRentalsViewModel : ObservableObject
{
    private readonly AssetBookingsClient _client;

    public MyAssetRentalsViewModel(AssetBookingsClient client) => _client = client;

    [ObservableProperty] private ObservableCollection<AssetBookingDto> bookings = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.MineAsync(null, 1, 100);
            Bookings = new ObservableCollection<AssetBookingDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load your rentals ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync(AssetBookingDto booking)
    {
        if (booking.Id is not int id || Shell.Current is null) return;

        var confirmed = await Shell.Current.DisplayAlert("Cancel Rental Request", "Cancel this rental request?", "Yes", "No");
        if (!confirmed) return;

        try
        {
            await _client.CancelAsync(id, null);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't cancel the rental request ({ex.Message}).";
        }
    }
}
