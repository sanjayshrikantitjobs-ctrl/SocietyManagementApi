using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Maintenance.Forms;

public partial class WaterTankerLogFormPage : ContentPage, IQueryAttributable
{
    private readonly WaterTankerLogFormViewModel _viewModel;

    public WaterTankerLogFormPage(WaterTankerLogFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("log", out var log) && log is WaterTankerLogDto dto) _viewModel.LoadFrom(dto);
    }
}
