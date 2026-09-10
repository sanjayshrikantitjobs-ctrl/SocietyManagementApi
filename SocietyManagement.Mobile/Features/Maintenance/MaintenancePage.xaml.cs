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

    // A SearchBar's SearchCommand only fires on Enter/the search-icon tap —
    // exactly what we want while the user is typing — but its built-in
    // clear ("x") button just empties Text without invoking SearchCommand,
    // silently leaving the last filtered results on screen. These handlers
    // catch only that empty-text case and reload immediately; any non-empty
    // change is left alone so search still requires Enter.
    private async void OnBillSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) await _viewModel.LoadBillsCommand.ExecuteAsync(null);
    }

    private async void OnSpecialChargeSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) await _viewModel.LoadSpecialChargesCommand.ExecuteAsync(null);
    }

    private async void OnFineSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) await _viewModel.LoadFinesCommand.ExecuteAsync(null);
    }

    private async void OnWaterTankerSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) await _viewModel.LoadWaterTankerCommand.ExecuteAsync(null);
    }
}
