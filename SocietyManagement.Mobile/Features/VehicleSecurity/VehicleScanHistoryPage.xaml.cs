namespace SocietyManagement.Mobile.Features.VehicleSecurity;

public partial class VehicleScanHistoryPage : ContentPage
{
    private readonly VehicleScanHistoryViewModel _viewModel;

    public VehicleScanHistoryPage(VehicleScanHistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
