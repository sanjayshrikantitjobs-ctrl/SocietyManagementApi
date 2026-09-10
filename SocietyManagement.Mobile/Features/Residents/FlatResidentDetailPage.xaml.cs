namespace SocietyManagement.Mobile.Features.Residents;

public partial class FlatResidentDetailPage : ContentPage, IQueryAttributable
{
    private readonly FlatResidentDetailViewModel _viewModel;

    public FlatResidentDetailPage(FlatResidentDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("occupancyType", out var occupancyType)) _viewModel.OccupancyType = (string)occupancyType;
        if (query.TryGetValue("flatNumber", out var flatNumber)) _viewModel.FlatNumber = (string)flatNumber;
        if (query.TryGetValue("flatId", out var flatId)) _viewModel.FlatId = (int)flatId;
    }
}
