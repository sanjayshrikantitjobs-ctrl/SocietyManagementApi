namespace SocietyManagement.Mobile.Features.Residents;

public partial class ResidentsListPage : ContentPage
{
    private readonly ResidentsListViewModel _viewModel;

    public ResidentsListPage(ResidentsListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
