using CommunityToolkit.Mvvm.ComponentModel;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Core.Auth;

/// <summary>Single source of truth for "who is signed in and what can they
/// do" — mirrors the Angular AuthService's signals (currentUser,
/// permissions, isSuperAdmin/isAdmin/isWatchman, hasPermission). Registered
/// as a singleton so every page/ViewModel observes the same instance; a
/// login/logout updates it once and every subscriber (nav menu, guards)
/// reacts. RoleName values match SocietyManagement.Shared.Constants.Roles
/// on the backend: SuperAdmin, Admin, Member, Watchman.</summary>
public partial class AuthState : ObservableObject
{
    [ObservableProperty]
    private UserProfileDto? currentUser;

    public bool IsAuthenticated => CurrentUser != null;

    public string? RoleName => CurrentUser?.RoleName;

    public bool IsSuperAdmin => RoleName == "SuperAdmin";

    public bool IsAdmin => RoleName is "Admin" or "SuperAdmin";

    public bool IsWatchman => RoleName == "Watchman";

    public bool IsMember => RoleName == "Member";

    /// <summary>Null only for SuperAdmin — every other role belongs to
    /// exactly one society (see JwtService.cs's society_id claim).</summary>
    public int? SocietyId => CurrentUser?.SocietyId;

    public bool HasPermission(string code) =>
        CurrentUser?.Permissions?.Contains(code, StringComparer.OrdinalIgnoreCase) == true;

    public void SetUser(UserProfileDto user)
    {
        CurrentUser = user;
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(RoleName));
        OnPropertyChanged(nameof(IsSuperAdmin));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsWatchman));
        OnPropertyChanged(nameof(IsMember));
    }

    public void Clear()
    {
        CurrentUser = null;
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(RoleName));
        OnPropertyChanged(nameof(IsSuperAdmin));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsWatchman));
        OnPropertyChanged(nameof(IsMember));
    }
}
