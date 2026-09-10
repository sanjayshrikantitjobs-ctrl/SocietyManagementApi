using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Committee;

public partial class CommitteePage : ContentPage
{
    private readonly CommitteeViewModel _viewModel;

    public CommitteePage(CommitteeViewModel viewModel, TopBarView topBar)
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
