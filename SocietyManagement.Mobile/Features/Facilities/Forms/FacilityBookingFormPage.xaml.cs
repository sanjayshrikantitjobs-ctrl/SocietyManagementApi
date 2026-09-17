using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Facilities.Forms;

public partial class FacilityBookingFormPage : ContentPage, IQueryAttributable
{
    private readonly FacilityBookingFormViewModel _viewModel;

    public FacilityBookingFormPage(FacilityBookingFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("facility", out var facility) && facility is FacilityDto dto)
        {
            var bookingDate = query.TryGetValue("bookingDate", out var date) && date is DateTime d ? d : DateTime.Today;
            _viewModel.LoadFacility(dto, bookingDate);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadFlatsAsync();
    }
}
