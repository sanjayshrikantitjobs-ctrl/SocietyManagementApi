namespace SocietyManagement.Mobile.Features.Maintenance.Forms;

public partial class FineFormPage : ContentPage, IQueryAttributable
{
    private readonly FineFormViewModel _viewModel;

    public FineFormPage(FineFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
    }
}
