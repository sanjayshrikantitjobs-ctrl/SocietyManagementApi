namespace SocietyManagement.Mobile.Features.Visitors;

public partial class VisitorSettingsPage : ContentPage
{
    private readonly VisitorSettingsViewModel _viewModel;

    public VisitorSettingsPage(VisitorSettingsViewModel viewModel)
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
