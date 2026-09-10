using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class DistributionFormPage : ContentPage, IQueryAttributable
{
    private readonly DistributionFormViewModel _viewModel;

    public DistributionFormPage(DistributionFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("distribution", out var distribution) && distribution is FestivalDistributionDto dto) _viewModel.LoadFrom(dto);
    }
}
