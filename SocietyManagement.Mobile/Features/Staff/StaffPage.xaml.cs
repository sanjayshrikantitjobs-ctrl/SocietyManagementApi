using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Staff;

public partial class StaffPage : ContentPage
{
    private readonly StaffViewModel _viewModel;

    public StaffPage(StaffViewModel viewModel, TopBarView topBar)
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
