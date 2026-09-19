using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core.Notifications;
using SocietyManagement.Mobile.Core.Signalr;

namespace SocietyManagement.Mobile.Features.Notifications;

/// <summary>Mirrors notification-panel.component.ts: loads the current
/// user's own notifications (server already scopes to the caller — see
/// NotificationsController), marks one read then deep-links via
/// NotificationLinkResolver, or marks all read. NotificationHubClient's
/// NotificationReceived event triggers a reload while this page is open so a
/// live push shows up without a manual pull-to-refresh, same as the web
/// panel's SignalR-driven re-fetch.</summary>
public partial class NotificationCenterViewModel : ObservableObject, IDisposable
{
    private readonly NotificationsClient _client;
    private readonly NotificationHubClient _hub;

    public NotificationCenterViewModel(NotificationsClient client, NotificationHubClient hub)
    {
        _client = client;
        _hub = hub;
        _hub.NotificationReceived += OnNotificationReceived;
    }

    [ObservableProperty] private ObservableCollection<NotificationDto> notifications = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    private void OnNotificationReceived() =>
        MainThread.BeginInvokeOnMainThread(() => _ = LoadCommand.ExecuteAsync(null));

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.NotificationsAsync(null, 1, 50);
            Notifications = new ObservableCollection<NotificationDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load notifications ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync(NotificationDto notification)
    {
        if (notification.Id is not int id) return;

        if (notification.IsRead != true)
        {
            try
            {
                await _client.MarkRead2Async(id);
                notification.IsRead = true;
                notification.ReadAt = DateTimeOffset.Now;
                var index = Notifications.ToList().FindIndex(n => n.Id == id);
                if (index >= 0) Notifications[index] = notification;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Couldn't mark as read ({ex.Message}).";
            }
        }

        var route = NotificationLinkResolver.Resolve(notification);
        if (route is not null && Shell.Current is not null) await Shell.Current.GoToAsync(route);
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        try
        {
            await _client.MarkAllReadAsync();
            foreach (var n in Notifications) { n.IsRead = true; n.ReadAt = DateTimeOffset.Now; }
            Notifications = new ObservableCollection<NotificationDto>(Notifications);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't mark all as read ({ex.Message}).";
        }
    }

    public void Dispose() => _hub.NotificationReceived -= OnNotificationReceived;
}
