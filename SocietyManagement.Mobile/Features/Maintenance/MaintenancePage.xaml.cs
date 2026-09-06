using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Maintenance;

public partial class MaintenancePage : ContentPage
{
    private readonly MaintenanceViewModel _viewModel;

    public MaintenancePage(MaintenanceViewModel viewModel, TopBarView topBar)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Shell.SetTitleView(this, topBar);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDashboardCommand.ExecuteAsync(null);
    }
}
