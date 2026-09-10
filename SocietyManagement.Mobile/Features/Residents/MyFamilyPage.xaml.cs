using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Residents;

public partial class MyFamilyPage : ContentPage
{
    private readonly MyFamilyViewModel _viewModel;

    public MyFamilyPage(MyFamilyViewModel viewModel, TopBarView topBar)
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
