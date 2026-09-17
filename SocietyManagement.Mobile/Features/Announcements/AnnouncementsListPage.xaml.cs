using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Announcements;

public partial class AnnouncementsListPage : ContentPage
{
    private readonly AnnouncementsListViewModel _viewModel;

    public AnnouncementsListPage(AnnouncementsListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnAnnouncementSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AnnouncementDto { Id: int id }) return;
        if (sender is CollectionView collectionView) collectionView.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(AnnouncementDetailPage)}?announcementId={id}");
    }
}
