namespace SocietyManagement.Mobile.Features.Maintenance;

public partial class MaintenanceDashboardPage : ContentPage
{
    private readonly MaintenanceDashboardViewModel _viewModel;

    public MaintenanceDashboardPage(MaintenanceDashboardViewModel viewModel)
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
