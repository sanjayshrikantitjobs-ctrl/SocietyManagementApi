namespace SocietyManagement.Mobile.Features.Festivals;

public partial class FestivalsListPage : ContentPage
{
    private readonly FestivalsListViewModel _viewModel;

    public FestivalsListPage(FestivalsListViewModel viewModel)
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
