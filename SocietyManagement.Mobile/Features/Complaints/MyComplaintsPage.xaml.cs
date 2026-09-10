using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Complaints;

public partial class MyComplaintsPage : ContentPage
{
    private readonly MyComplaintsViewModel _viewModel;

    public MyComplaintsPage(MyComplaintsViewModel viewModel, TopBarView topBar)
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
