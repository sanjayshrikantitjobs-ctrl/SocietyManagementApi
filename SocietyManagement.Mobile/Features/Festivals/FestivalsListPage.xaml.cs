using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Festivals;

public partial class FestivalsListPage : ContentPage
{
    private readonly FestivalsListViewModel _viewModel;

    public FestivalsListPage(FestivalsListViewModel viewModel, TopBarView topBar)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Shell.SetTitleView(this, topBar);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnFestivalSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not FestivalDto { Id: int id }) return;

        if (sender is CollectionView collectionView) collectionView.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(FestivalDetailPage)}?festivalId={id}");
    }
}
