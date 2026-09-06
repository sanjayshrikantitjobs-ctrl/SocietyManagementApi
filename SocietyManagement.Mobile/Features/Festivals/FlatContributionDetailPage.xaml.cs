namespace SocietyManagement.Mobile.Features.Festivals;

public partial class FlatContributionDetailPage : ContentPage, IQueryAttributable
{
    private readonly FlatContributionDetailViewModel _viewModel;

    public FlatContributionDetailPage(FlatContributionDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("flatNumber", out var flatNumber)) _viewModel.FlatNumber = flatNumber?.ToString() ?? string.Empty;
        if (query.TryGetValue("flatId", out var flatId)) _viewModel.FlatId = (int)flatId;
    }
}
