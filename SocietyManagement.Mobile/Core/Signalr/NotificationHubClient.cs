using Microsoft.AspNetCore.SignalR.Client;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Core.Signalr;

/// <summary>The mobile equivalent of Web's SignalrService — connects to the
/// exact same hub (ApiConfig.HubUrl = /hubs/notifications) the backend
/// already serves, with the JWT passed the same way the hub's own doc
/// comment expects (bearer token, here via SignalR's AccessTokenProvider
/// rather than a query string — the .NET client handles that translation
/// itself). No new backend surface, no second delivery channel.
///
/// Deliberately generic/reusable: `On&lt;T&gt;` lets any future feature (SOS,
/// etc.) register its own typed handler without touching this class: only
/// the Notification Center's own "did an event I care about just arrive"
/// wiring lives here as NotificationReceived.</summary>
public class NotificationHubClient : IAsyncDisposable
{
    /// <summary>Mirrors Web SignalrService's own hardcoded event list —
    /// every event name NotificationService can raise (see
    /// INotificationService's doc comment on the backend). Kept here, not
    /// derived, since there's no shared contract between the two clients
    /// beyond the string names themselves.</summary>
    private static readonly string[] KnownEventNames =
    {
        "AnnouncementPublished",
        "ComplaintRaised", "ComplaintAssigned", "ComplaintInProgress", "ComplaintResolved", "ComplaintReopened",
        "VisitorApprovalRequested", "VisitorApproved", "VisitorRejected", "VisitorRequestExpired",
        "FestivalContributionRecorded", "FestivalExpenseApproved",
        "SupportTicketCreated", "SupportTicketResolved"
    };

    private readonly HubConnection _connection;

    public NotificationHubClient(ITokenStorage tokenStorage)
    {
        // Built here, not in StartAsync, so On<T> is always safe to call
        // regardless of whether the connection has started yet — a future
        // feature registering its own handler during app startup shouldn't
        // have to know or care about connection timing.
        _connection = new HubConnectionBuilder()
            .WithUrl(ApiConfig.HubUrl, options =>
            {
                options.AccessTokenProvider = () => tokenStorage.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        foreach (var eventName in KnownEventNames)
        {
            _connection.On<object>(eventName, _ => NotificationReceived?.Invoke());
        }
    }

    /// <summary>Fires whenever any known notification-worthy event arrives
    /// while connected — the Notification Center badge re-fetches the
    /// authoritative unread count from the server on this, the same
    /// "don't trust a local increment, ask the source of truth" choice
    /// Web's main-layout makes.</summary>
    public event Action? NotificationReceived;

    public bool IsConnected => _connection.State == HubConnectionState.Connected;

    public async Task StartAsync()
    {
        if (_connection.State != HubConnectionState.Disconnected) return;

        try
        {
            await _connection.StartAsync();
        }
        catch
        {
            // Best-effort: a failed connection (offline, token expired) just
            // means no live push until the next explicit StartAsync — the
            // persisted Notification Center list is still reachable via the
            // ordinary REST call regardless.
        }
    }

    /// <summary>Registers a handler for one event name without the caller
    /// needing to touch this class's own fixed KnownEventNames list — the
    /// reusable hook for a future real-time feature (e.g. SOS) that isn't
    /// part of the Notification Center's own badge-refresh concern. Safe
    /// to call before StartAsync — the connection object already exists.</summary>
    public void On<T>(string eventName, Action<T> handler)
    {
        _connection.On(eventName, handler);
    }

    public async Task StopAsync()
    {
        if (_connection.State == HubConnectionState.Disconnected) return;
        await _connection.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _connection.DisposeAsync();
    }
}
