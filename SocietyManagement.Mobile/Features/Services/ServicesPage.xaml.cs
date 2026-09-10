using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Services;

public partial class ServicesPage : ContentPage
{
    private readonly ServicesViewModel _viewModel;

    public ServicesPage(ServicesViewModel viewModel, TopBarView topBar)
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
