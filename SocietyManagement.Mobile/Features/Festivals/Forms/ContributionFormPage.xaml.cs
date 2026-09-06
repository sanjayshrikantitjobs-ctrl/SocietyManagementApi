using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class ContributionFormPage : ContentPage, IQueryAttributable
{
    private readonly ContributionFormViewModel _viewModel;

    public ContributionFormPage(ContributionFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("lockedFlatId", out var flatId)) _viewModel.LockedFlatId = (int)flatId;
        if (query.TryGetValue("lockedFlatNumber", out var flatNumber)) _viewModel.LockedFlatNumber = flatNumber?.ToString();
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("contribution", out var contribution) && contribution is FestivalContributionDto dto) _viewModel.LoadFrom(dto);
    }
}
