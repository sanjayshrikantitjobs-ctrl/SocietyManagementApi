namespace SocietyManagement.Mobile.Features.Societies;

public partial class ParkingSlotsPage : ContentPage, IQueryAttributable
{
    private readonly ParkingSlotsViewModel _viewModel;

    public ParkingSlotsPage(ParkingSlotsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
    }
}
