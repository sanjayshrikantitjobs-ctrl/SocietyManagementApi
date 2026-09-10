using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Finance;

public partial class FinancePage : ContentPage
{
    private readonly FinanceViewModel _viewModel;

    public FinancePage(FinanceViewModel viewModel, TopBarView topBar)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Shell.SetTitleView(this, topBar);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.SelectTabCommand.ExecuteAsync(_viewModel.SelectedTab);
    }
}
