namespace SocietyManagement.Mobile.Features.Festivals;

public partial class DistributionDetailPage : ContentPage, IQueryAttributable
{
    private readonly DistributionDetailViewModel _viewModel;

    public DistributionDetailPage(DistributionDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("distributionId", out var distributionId)) _viewModel.DistributionId = (int)distributionId;
    }
}
