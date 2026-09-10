namespace SocietyManagement.Mobile.Features.Residents.Forms;

public partial class OccupancyMemberFormPage : ContentPage, IQueryAttributable
{
    private readonly OccupancyMemberFormViewModel _viewModel;

    public OccupancyMemberFormPage(OccupancyMemberFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("mode", out var mode)) _viewModel.Mode = (string)mode;
        if (query.TryGetValue("flatId", out var flatId)) _viewModel.FlatId = (int)flatId;
        if (query.TryGetValue("flatOccupancyId", out var occId)) _viewModel.FlatOccupancyId = (int)occId;
        if (query.TryGetValue("personId", out var personId)) _viewModel.PersonId = (int)personId;
    }
}
