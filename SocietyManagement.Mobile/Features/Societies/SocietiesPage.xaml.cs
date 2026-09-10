using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Societies;

public partial class SocietiesPage : ContentPage
{
    private readonly SocietiesViewModel _viewModel;

    public SocietiesPage(SocietiesViewModel viewModel, TopBarView topBar)
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
