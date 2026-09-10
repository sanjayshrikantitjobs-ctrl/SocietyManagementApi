using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Residents.Forms;

public partial class EmergencyContactFormPage : ContentPage, IQueryAttributable
{
    private readonly EmergencyContactFormViewModel _viewModel;

    public EmergencyContactFormPage(EmergencyContactFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("flatId", out var flatId)) _viewModel.FlatId = (int)flatId;
        if (query.TryGetValue("contact", out var contact) && contact is EmergencyContactDto dto) _viewModel.LoadFrom(dto);
    }
}
