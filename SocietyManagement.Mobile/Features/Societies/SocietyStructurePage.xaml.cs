namespace SocietyManagement.Mobile.Features.Societies;

public partial class SocietyStructurePage : ContentPage, IQueryAttributable
{
    private readonly SocietyStructureViewModel _viewModel;

    public SocietyStructurePage(SocietyStructureViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
    }
}
