namespace SocietyManagement.Mobile.Features.Facilities;

public partial class MyFacilityBookingsPage : ContentPage
{
    private readonly MyFacilityBookingsViewModel _viewModel;

    public MyFacilityBookingsPage(MyFacilityBookingsViewModel viewModel)
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
