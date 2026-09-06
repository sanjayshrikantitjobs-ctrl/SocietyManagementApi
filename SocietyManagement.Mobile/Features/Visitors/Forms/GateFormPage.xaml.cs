using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Visitors.Forms;

public partial class GateFormPage : ContentPage, IQueryAttributable
{
    private readonly GateFormViewModel _viewModel;

    public GateFormPage(GateFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("gate", out var gate) && gate is GateDto dto) _viewModel.LoadFrom(dto);
    }
}
