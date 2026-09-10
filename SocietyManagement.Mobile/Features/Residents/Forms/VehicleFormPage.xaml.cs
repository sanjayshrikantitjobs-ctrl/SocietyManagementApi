using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Residents.Forms;

public partial class VehicleFormPage : ContentPage, IQueryAttributable
{
    private readonly VehicleFormViewModel _viewModel;

    public VehicleFormPage(VehicleFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("flatId", out var flatId)) _viewModel.FlatId = (int)flatId;
        if (query.TryGetValue("vehicle", out var vehicle) && vehicle is VehicleDto dto) _viewModel.LoadFrom(dto);
    }
}
