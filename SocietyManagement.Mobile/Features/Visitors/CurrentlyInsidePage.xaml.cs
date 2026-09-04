namespace SocietyManagement.Mobile.Features.Visitors;

public partial class CurrentlyInsidePage : ContentPage
{
    private readonly CurrentlyInsideViewModel _viewModel;

    public CurrentlyInsidePage(CurrentlyInsideViewModel viewModel)
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
