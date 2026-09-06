using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Residents;

public partial class ResidentsListPage : ContentPage
{
    private readonly ResidentsListViewModel _viewModel;

    public ResidentsListPage(ResidentsListViewModel viewModel, TopBarView topBar)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Shell.SetTitleView(this, topBar);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
