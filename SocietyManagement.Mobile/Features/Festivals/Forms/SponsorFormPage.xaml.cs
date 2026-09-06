using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class SponsorFormPage : ContentPage, IQueryAttributable
{
    private readonly SponsorFormViewModel _viewModel;

    public SponsorFormPage(SponsorFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("sponsor", out var sponsor) && sponsor is FestivalSponsorDto dto) _viewModel.LoadFrom(dto);
    }
}
