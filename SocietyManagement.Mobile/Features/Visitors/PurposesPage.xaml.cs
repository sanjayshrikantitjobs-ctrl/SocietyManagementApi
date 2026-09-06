namespace SocietyManagement.Mobile.Features.Visitors;

public partial class PurposesPage : ContentPage
{
    private readonly PurposesViewModel _viewModel;

    public PurposesPage(PurposesViewModel viewModel)
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
