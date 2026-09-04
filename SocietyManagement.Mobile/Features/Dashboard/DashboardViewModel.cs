using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Auth;

namespace SocietyManagement.Mobile.Features.Dashboard;

/// <summary>Placeholder landing page proving the login -> authenticated API
/// call round trip end to end. Real role-specific dashboards (admin-summary
/// / member-summary, matching DashboardController) land in Phase 1+ once
/// each role's actual modules exist to navigate to.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly AuthState _authState;

    public DashboardViewModel(IAuthService authService, AuthState authState)
    {
        _authService = authService;
        _authState = authState;
    }

    public string FullName => $"{_authState.CurrentUser?.FirstName} {_authState.CurrentUser?.LastName}".Trim();

    public string RoleName => _authState.CurrentUser?.RoleName ?? string.Empty;

    public string Email => _authState.CurrentUser?.Email ?? string.Empty;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }
}
