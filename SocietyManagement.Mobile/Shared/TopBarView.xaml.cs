using System.Runtime.CompilerServices;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Core.Signalr;
using SocietyManagement.Mobile.Features.Auth;
using SocietyManagement.Mobile.Features.Notifications;

namespace SocietyManagement.Mobile.Shared;

/// <summary>Reusable top bar mirroring the web app's own topbar
/// (main-layout.component.ts): society name/address on the left,
/// notifications/theme-toggle/user-menu on the right. Set as a page's
/// Shell.TitleView (see DashboardPage.xaml and friends) rather than each
/// page carrying its own header markup — one place to keep this in parity
/// with the web app as more of it becomes real (notifications currently
/// route to a placeholder; SignalR-backed live notifications are a
/// follow-up, same as the rest of the real-time piece in the mobile plan).
/// Property-change notification uses BindableObject's own OnPropertyChanged
/// (inherited from ContentView) — no separate INotifyPropertyChanged
/// implementation needed since ContentView already provides one.</summary>
public partial class TopBarView : ContentView
{
    private readonly CurrentSocietyService? _currentSocietyService;
    private readonly AuthState? _authState;
    private readonly IAuthService? _authService;
    private readonly NotificationHubClient? _notificationHub;
    private readonly NotificationsClient? _notificationsClient;

    private string _societyName = "Society Management";
    private string? _societyAddress;
    private string _themeIcon = "\U0001F319"; // 🌙
    private int _unreadNotificationCount;

    public string SocietyName
    {
        get => _societyName;
        set { _societyName = value; RaisePropertyChanged(); }
    }

    public string? SocietyAddress
    {
        get => _societyAddress;
        set { _societyAddress = value; RaisePropertyChanged(); }
    }

    public string ThemeIcon
    {
        get => _themeIcon;
        set { _themeIcon = value; RaisePropertyChanged(); }
    }

    public int UnreadNotificationCount
    {
        get => _unreadNotificationCount;
        set { _unreadNotificationCount = value; RaisePropertyChanged(); }
    }

    public TopBarView()
    {
        InitializeComponent();
    }

    public TopBarView(CurrentSocietyService currentSocietyService, AuthState authState, IAuthService authService,
        NotificationHubClient notificationHub, NotificationsClient notificationsClient) : this()
    {
        _currentSocietyService = currentSocietyService;
        _authState = authState;
        _authService = authService;
        _notificationHub = notificationHub;
        _notificationsClient = notificationsClient;
        UpdateThemeIcon();
        Loaded += async (_, _) => await LoadSocietyAsync();
        Loaded += async (_, _) => await LoadUnreadCountAsync();
        // NotificationReceived fires for every open TopBarView instance since
        // NotificationHubClient is a Singleton — cheap (one int fetch) and
        // keeps the badge live on whichever page is currently shown, same as
        // Web's main-layout re-fetching its own badge on the same signal.
        _notificationHub.NotificationReceived += OnNotificationReceived;
        Unloaded += (_, _) => _notificationHub.NotificationReceived -= OnNotificationReceived;
    }

    private void OnNotificationReceived() =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadUnreadCountAsync());

    private async Task LoadUnreadCountAsync()
    {
        if (_notificationsClient is null) return;
        try
        {
            var response = await _notificationsClient.UnreadCount2Async();
            UnreadNotificationCount = response.Data ?? 0;
        }
        catch
        {
            // Badge is cosmetic — a failed lookup just leaves the last known
            // count (0 on first load), never blocks the top bar.
        }
    }

    private async Task LoadSocietyAsync()
    {
        if (_currentSocietyService is null) return;
        try
        {
            var society = await _currentSocietyService.GetSocietyAsync();
            if (society is null) return;
            SocietyName = society.Name ?? SocietyName;
            SocietyAddress = string.Join(", ", new[] { society.Address, society.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch
        {
            // Top bar branding is cosmetic — a failed lookup just keeps the
            // generic "Society Management" title, never blocks the page.
        }
    }

    private async void OnNotificationsTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(NotificationCenterPage));
    }

    private void OnThemeToggleTapped(object? sender, TappedEventArgs e)
    {
        if (Application.Current is null) return;
        Application.Current.UserAppTheme = Application.Current.RequestedTheme == AppTheme.Dark
            ? AppTheme.Light
            : AppTheme.Dark;
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        ThemeIcon = isDark ? "☀" : "\U0001F319"; // ☀ when dark (tap to go light) / 🌙 when light (tap to go dark)
    }

    private async void OnUserMenuTapped(object? sender, TappedEventArgs e)
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is not Page page) return;

        var fullName = $"{_authState?.CurrentUser?.FirstName} {_authState?.CurrentUser?.LastName}".Trim();
        var roleName = _authState?.CurrentUser?.RoleName ?? string.Empty;
        var choice = await page.DisplayActionSheet($"{fullName} ({roleName})", "Cancel", null, "Change Password", "Logout");

        switch (choice)
        {
            case "Change Password":
                await Shell.Current.GoToAsync(nameof(ChangePasswordPage));
                break;
            case "Logout":
                if (_authService is not null)
                {
                    await _authService.LogoutAsync();
                    await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
                }
                break;
        }
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        OnPropertyChanged(propertyName);
}
