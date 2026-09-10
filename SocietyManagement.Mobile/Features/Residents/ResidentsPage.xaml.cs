using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Residents;

public partial class ResidentsPage : ContentPage
{
    private readonly ResidentsViewModel _viewModel;

    public ResidentsPage(ResidentsViewModel viewModel, TopBarView topBar)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Shell.SetTitleView(this, topBar);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.SelectTabCommand.ExecuteAsync(_viewModel.SelectedTab);
    }
}
