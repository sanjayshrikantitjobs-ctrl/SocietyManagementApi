using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Dashboard;

namespace SocietyManagement.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;

    public AppShell(IAuthService authService, AuthState authState)
    {
        InitializeComponent();
        _authService = authService;
        // Drives every FlyoutItem's role-based IsVisible binding (see
        // AppShell.xaml) — the same AuthState instance login/logout update,
        // so the flyout refreshes itself the moment the signed-in role changes.
        BindingContext = authState;
        Loaded += OnLoaded;
    }

    /// <summary>Mirrors app.config.ts's provideAppInitializer — restores a
    /// still-valid session (stored refresh token) before the user would
    /// otherwise see the Login page, same as the web app not flashing a
    /// login screen on a page refresh.</summary>
    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (await _authService.RestoreSessionAsync())
        {
            await GoToAsync($"//{nameof(DashboardPage)}");
        }
    }
}
