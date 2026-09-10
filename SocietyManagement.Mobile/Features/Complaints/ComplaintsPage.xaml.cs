using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Complaints;

public partial class ComplaintsPage : ContentPage
{
    private readonly ComplaintsViewModel _viewModel;

    public ComplaintsPage(ComplaintsViewModel viewModel, TopBarView topBar)
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
