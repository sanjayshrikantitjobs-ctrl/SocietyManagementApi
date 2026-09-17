namespace SocietyManagement.Mobile.Features.Assets;

public partial class MyAssetRentalsPage : ContentPage
{
    private readonly MyAssetRentalsViewModel _viewModel;

    public MyAssetRentalsPage(MyAssetRentalsViewModel viewModel)
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
