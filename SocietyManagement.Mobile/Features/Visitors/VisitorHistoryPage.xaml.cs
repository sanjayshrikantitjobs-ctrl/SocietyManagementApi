namespace SocietyManagement.Mobile.Features.Visitors;

public partial class VisitorHistoryPage : ContentPage
{
    private readonly VisitorHistoryViewModel _viewModel;

    public VisitorHistoryPage(VisitorHistoryViewModel viewModel)
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
