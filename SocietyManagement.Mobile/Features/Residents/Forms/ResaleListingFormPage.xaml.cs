using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Residents.Forms;

public partial class ResaleListingFormPage : ContentPage, IQueryAttributable
{
    private readonly ResaleListingFormViewModel _viewModel;
    private FlatResaleListingDto? _pendingListing;

    public ResaleListingFormPage(ResaleListingFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Member options must be loaded before LoadFrom can resolve
        // SelectedMember by id — ApplyQueryAttributes (below) runs before
        // OnAppearing, so the listing itself is only applied here, after.
        await _viewModel.LoadMemberOptionsAsync();
        if (_pendingListing is not null)
        {
            _viewModel.LoadFrom(_pendingListing);
            _pendingListing = null;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("flatId", out var flatId)) _viewModel.FlatId = (int)flatId;
        if (query.TryGetValue("listing", out var listing) && listing is FlatResaleListingDto dto) _pendingListing = dto;
    }
}
