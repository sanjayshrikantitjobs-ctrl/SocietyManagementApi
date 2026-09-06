namespace SocietyManagement.Mobile.Features.Visitors;

public partial class GatesPage : ContentPage
{
    private readonly GatesViewModel _viewModel;

    public GatesPage(GatesViewModel viewModel)
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
