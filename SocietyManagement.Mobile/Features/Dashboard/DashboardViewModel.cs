using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Maintenance;
using SocietyManagement.Mobile.Features.Residents;
using SocietyManagement.Mobile.Features.VehicleSecurity;
using SocietyManagement.Mobile.Features.Visitors;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Dashboard;

/// <summary>The "Home" landing page — a flex grid of module tiles, mirroring
/// the shape of the web app's own nav (main-layout.component.ts's
/// NAV_ITEMS), gated the same way (AuthState.IsAdmin/IsWatchman). Modules
/// without a real screen yet route to ComingSoonPage rather than being
/// omitted from the grid. Logout lives in TopBarView's user menu (matching
/// the web app's avatar dropdown), not on this page.</summary>
public partial class DashboardViewModel : ObservableObject
{
    public DashboardViewModel(AuthState authState)
    {
        Auth = authState;
    }

    /// <summary>Exposed directly (not mirrored into local properties) so
    /// every tile's IsVisible binding — {Binding Auth.IsAdmin} etc. —
    /// updates live if the signed-in role ever changes.</summary>
    public AuthState Auth { get; }

    public string FullName => $"{Auth.CurrentUser?.FirstName} {Auth.CurrentUser?.LastName}".Trim();

    [RelayCommand]
    private async Task NavigateAsync(string target)
    {
        // Residents/Maintenance/Visitors/Vehicles are each a FlyoutItem's own
        // ShellContent — a top-level shell element, which MAUI Shell requires
        // an absolute "//" route to reach (relative routing to a shell
        // element throws). ContactUsPage/ComingSoonPage are plain pushed
        // pages (Routing.RegisterRoute only), so they stay relative.
        switch (target)
        {
            case "Residents":
                await Shell.Current.GoToAsync($"//{nameof(ResidentsListPage)}");
                break;
            case "Maintenance":
                await Shell.Current.GoToAsync($"//{nameof(MaintenancePage)}");
                break;
            case "Visitors":
                await Shell.Current.GoToAsync($"//{nameof(NewVisitorPage)}");
                break;
            case "Vehicles":
                await Shell.Current.GoToAsync($"//{nameof(VehicleScanPage)}");
                break;
            case "Contact Us":
                await Shell.Current.GoToAsync(nameof(ContactUsPage));
                break;
            default:
                await AppShell.GoToComingSoonAsync(target);
                break;
        }
    }
}
