using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Support;

public partial class SupportTicketsPage : ContentPage
{
    private readonly SupportTicketsViewModel _viewModel;

    public SupportTicketsPage(SupportTicketsViewModel viewModel, TopBarView topBar)
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
