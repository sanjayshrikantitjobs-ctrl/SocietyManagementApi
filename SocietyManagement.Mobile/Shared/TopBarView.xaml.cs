using System.Runtime.CompilerServices;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Auth;

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

    private string _societyName = "Society Management";
    private string? _societyAddress;
    private string _themeIcon = "\U0001F319"; // 🌙

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

    public TopBarView()
    {
        InitializeComponent();
    }

    public TopBarView(CurrentSocietyService currentSocietyService, AuthState authState, IAuthService authService) : this()
    {
        _currentSocietyService = currentSocietyService;
        _authState = authState;
        _authService = authService;
        UpdateThemeIcon();
        Loaded += async (_, _) => await LoadSocietyAsync();
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
        await AppShell.GoToComingSoonAsync("Notifications");
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
