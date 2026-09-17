namespace SocietyManagement.Mobile.Features.Facilities;

[QueryProperty(nameof(FacilityIdParam), "facilityId")]
public partial class FacilityDetailPage : ContentPage
{
    private readonly FacilityDetailViewModel _viewModel;

    public FacilityDetailPage(FacilityDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string FacilityIdParam
    {
        set { if (int.TryParse(value, out var id)) _viewModel.FacilityId = id; }
    }
}
