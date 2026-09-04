namespace SocietyManagement.Mobile.Features.VehicleSecurity;

public partial class VehicleScanPage : ContentPage
{
    private readonly VehicleScanViewModel _viewModel;

    public VehicleScanPage(VehicleScanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.ResetCommand.Execute(null);
    }
}
