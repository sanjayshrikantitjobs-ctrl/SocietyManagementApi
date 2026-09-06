using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Maintenance.Forms;

public partial class SpecialChargeFormPage : ContentPage, IQueryAttributable
{
    private readonly SpecialChargeFormViewModel _viewModel;

    public SpecialChargeFormPage(SpecialChargeFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("charge", out var charge) && charge is SpecialChargeDto dto) _viewModel.LoadFrom(dto);
    }
}
