using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Notifications;

public partial class NotificationCenterPage : ContentPage
{
    private readonly NotificationCenterViewModel _viewModel;

    public NotificationCenterPage(NotificationCenterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnNotificationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not NotificationDto notification) return;
        if (sender is CollectionView collectionView) collectionView.SelectedItem = null;
        await _viewModel.OpenCommand.ExecuteAsync(notification);
    }
}
