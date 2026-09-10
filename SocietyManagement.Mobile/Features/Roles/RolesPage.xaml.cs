using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Roles;

public partial class RolesPage : ContentPage
{
    private readonly RolesViewModel _viewModel;

    public RolesPage(RolesViewModel viewModel, TopBarView topBar)
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
}
