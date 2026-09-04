using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.ParkingFines;

public partial class ParkingFinesPage : ContentPage
{
    private readonly ParkingFinesViewModel _viewModel;
    private CancellationTokenSource? _searchDebounce;

    public ParkingFinesPage(ParkingFinesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnVehicleSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);
                if (!token.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => _viewModel.SearchVehiclesCommand.ExecuteAsync(null));
                }
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private void OnVehicleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is VehicleSearchItemDto vehicle)
        {
            _viewModel.SelectVehicleCommand.Execute(vehicle);
        }
    }
}
