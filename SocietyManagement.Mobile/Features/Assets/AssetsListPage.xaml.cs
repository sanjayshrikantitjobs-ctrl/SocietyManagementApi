namespace SocietyManagement.Mobile.Features.Assets;

public partial class AssetsListPage : ContentPage
{
    private readonly AssetsListViewModel _viewModel;

    public AssetsListPage(AssetsListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
