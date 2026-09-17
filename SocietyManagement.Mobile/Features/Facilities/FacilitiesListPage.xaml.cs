using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Facilities;

public partial class FacilitiesListPage : ContentPage
{
    private readonly FacilitiesListViewModel _viewModel;

    public FacilitiesListPage(FacilitiesListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnFacilitySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not FacilityDto { Id: int id }) return;
        if (sender is CollectionView collectionView) collectionView.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(FacilityDetailPage)}?facilityId={id}");
    }
}
