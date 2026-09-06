using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Visitors.Forms;

public partial class PurposeFormPage : ContentPage, IQueryAttributable
{
    private readonly PurposeFormViewModel _viewModel;

    public PurposeFormPage(PurposeFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("purpose", out var purpose) && purpose is VisitorPurposeDto dto) _viewModel.LoadFrom(dto);
    }
}
