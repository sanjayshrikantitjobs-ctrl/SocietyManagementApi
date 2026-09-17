using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Assets;

public partial class AssetCartLine : ObservableObject
{
    public AssetCartLine(AssetDto asset) => Asset = asset;

    public AssetDto Asset { get; }

    [ObservableProperty] private int quantity = 1;
}

/// <summary>Mirrors assets-list.component.ts's resident "Rent Assets" flow:
/// pick a date range, choose quantities, add multiple assets to a cart,
/// submit one rental request. Server always recomputes the authoritative
/// price and the available-quantity guard — see AssetBookingFeature.
/// CreateAssetBookingCommandHandler.</summary>
public partial class AssetsListViewModel : ObservableObject
{
    private readonly AssetsClient _assetsClient;
    private readonly AssetBookingsClient _bookingsClient;
    private readonly FlatsClient _flatsClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public AssetsListViewModel(
        AssetsClient assetsClient, AssetBookingsClient bookingsClient, FlatsClient flatsClient, CurrentSocietyService currentSocietyService)
    {
        _assetsClient = assetsClient;
        _bookingsClient = bookingsClient;
        _flatsClient = flatsClient;
        _currentSocietyService = currentSocietyService;
        StartDate = DateTime.Today;
        EndDate = DateTime.Today;
    }

    [ObservableProperty] private ObservableCollection<AssetDto> assets = new();
    [ObservableProperty] private ObservableCollection<AssetCartLine> cart = new();
    [ObservableProperty] private ObservableCollection<FlatDto> flats = new();
    [ObservableProperty] private FlatDto? selectedFlat;
    [ObservableProperty] private DateTime startDate;
    [ObservableProperty] private DateTime endDate;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public decimal EstimatedTotal => Cart.Sum(line =>
    {
        var days = Math.Max((EndDate.Date - StartDate.Date).Days + 1, 1);
        var multiplier = line.Asset.PricingType is AssetPricingType.PerItem or AssetPricingType.Lumpsum ? 1 : days;
        return (decimal)(line.Asset.RentalPrice ?? 0) * line.Quantity * multiplier;
    });

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _assetsClient.AssetsGETAsync(societyId, true);
            Assets = new ObservableCollection<AssetDto>(response.Data ?? new());

            var flatsResponse = await _flatsClient.Mine5Async();
            Flats = new ObservableCollection<FlatDto>(flatsResponse.Data ?? new());
            if (Flats.Count > 0) SelectedFlat = Flats[0];
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load assets ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddToCart(AssetDto asset)
    {
        var existing = Cart.FirstOrDefault(l => l.Asset.Id == asset.Id);
        if (existing is not null)
        {
            existing.Quantity++;
        }
        else
        {
            Cart.Add(new AssetCartLine(asset));
        }
        OnPropertyChanged(nameof(EstimatedTotal));
    }

    [RelayCommand]
    private void DecrementCartLine(AssetCartLine line)
    {
        if (line.Quantity > 1) line.Quantity--;
        else Cart.Remove(line);
        OnPropertyChanged(nameof(EstimatedTotal));
    }

    [RelayCommand]
    private void RemoveFromCart(AssetCartLine line)
    {
        Cart.Remove(line);
        OnPropertyChanged(nameof(EstimatedTotal));
    }

    partial void OnStartDateChanged(DateTime value) => OnPropertyChanged(nameof(EstimatedTotal));
    partial void OnEndDateChanged(DateTime value) => OnPropertyChanged(nameof(EstimatedTotal));

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Cart.Count == 0 || SelectedFlat?.Id is not int flatId)
        {
            ErrorMessage = "Add at least one asset and select a flat.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var items = new ObservableCollection<AssetBookingItemInput>(
                Cart.Select(l => new AssetBookingItemInput { AssetId = l.Asset.Id, Quantity = l.Quantity }));

            await _bookingsClient.AssetBookingsPOSTAsync(new CreateAssetBookingCommand
            {
                FlatId = flatId, StartDate = new DateTimeOffset(StartDate.Date, TimeSpan.Zero),
                EndDate = new DateTimeOffset(EndDate.Date, TimeSpan.Zero), Notes = null, FacilityBookingId = null, Items = items
            });

            Cart = new ObservableCollection<AssetCartLine>();
            OnPropertyChanged(nameof(EstimatedTotal));
            if (Shell.Current is not null) await Shell.Current.GoToAsync("//MyAssetRentalsPage");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't submit the rental request ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
