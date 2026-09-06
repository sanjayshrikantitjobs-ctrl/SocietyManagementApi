using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class VendorFormPage : ContentPage, IQueryAttributable
{
    private readonly VendorFormViewModel _viewModel;

    public VendorFormPage(VendorFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("vendor", out var vendor) && vendor is FestivalVendorDto dto) _viewModel.LoadFrom(dto);
    }
}
