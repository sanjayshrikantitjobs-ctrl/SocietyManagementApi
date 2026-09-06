using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class VolunteerFormPage : ContentPage, IQueryAttributable
{
    private readonly VolunteerFormViewModel _viewModel;

    public VolunteerFormPage(VolunteerFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("volunteer", out var volunteer) && volunteer is FestivalVolunteerDto dto) _viewModel.LoadFrom(dto);
    }
}
