using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Committee;
using SocietyManagement.Mobile.Features.Complaints;
using SocietyManagement.Mobile.Features.Finance;
using SocietyManagement.Mobile.Features.Maintenance;
using SocietyManagement.Mobile.Features.Residents;
using SocietyManagement.Mobile.Features.Roles;
using SocietyManagement.Mobile.Features.Services;
using SocietyManagement.Mobile.Features.Societies;
using SocietyManagement.Mobile.Features.Staff;
using SocietyManagement.Mobile.Features.Users;
using SocietyManagement.Mobile.Features.VehicleSecurity;
using SocietyManagement.Mobile.Features.Visitors;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Dashboard;

/// <summary>The "Home" landing page. For Admin/SuperAdmin it opens with the
/// same KPI + "Requires Attention" + charts summary as the web's own Admin
/// Dashboard (admin-summary/monthly-collection-trend endpoints,
/// DashboardController.cs), then falls into the flex grid of module tiles
/// below it — mirroring the web's own nav (main-layout.component.ts's
/// NAV_ITEMS), gated the same way (AuthState.IsAdmin/IsWatchman). Modules
/// without a real screen yet route to ComingSoonPage rather than being
/// omitted from the grid. Logout lives in TopBarView's user menu (matching
/// the web app's avatar dropdown), not on this page.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly DashboardClient _dashboardClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public DashboardViewModel(AuthState authState, DashboardClient dashboardClient, CurrentSocietyService currentSocietyService)
    {
        Auth = authState;
        _dashboardClient = dashboardClient;
        _currentSocietyService = currentSocietyService;
    }

    /// <summary>Exposed directly (not mirrored into local properties) so
    /// every tile's IsVisible binding — {Binding Auth.IsAdmin} etc. —
    /// updates live if the signed-in role ever changes.</summary>
    public AuthState Auth { get; }

    public string FullName => $"{Auth.CurrentUser?.FirstName} {Auth.CurrentUser?.LastName}".Trim();

    [ObservableProperty] private bool isSummaryBusy;
    [ObservableProperty] private AdminDashboardSummaryDto? summary;
    [ObservableProperty] private Chart? occupancyChart;
    [ObservableProperty] private Chart? collectionChart;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Auth.IsAdmin) return;

        IsSummaryBusy = true;
        try
        {
            var societyId = await _currentSocietyService.GetSocietyIdAsync();
            if (societyId is null) return;

            var summaryResponse = await _dashboardClient.AdminSummaryAsync(societyId);
            Summary = summaryResponse.Data;

            if (Summary != null)
            {
                var occupied = Summary.OccupiedFlats ?? 0;
                var vacant = Math.Max((Summary.TotalFlats ?? 0) - occupied, 0);
                OccupancyChart = ChartFactory.BuildProportionDonut(
                    "Occupied", occupied, ChartFactory.ColorInfo,
                    "Vacant", vacant, ChartFactory.ColorMuted);
            }

            var trendResponse = await _dashboardClient.MonthlyCollectionTrendAsync(societyId, 6);
            var points = (trendResponse.Data ?? new())
                .Select(p => (p.MonthLabel, p.Collected ?? 0, p.Pending ?? 0))
                .ToList();
            CollectionChart = ChartFactory.BuildPairedBar(points, ChartFactory.ColorSuccess, ChartFactory.ColorWarning);
        }
        catch
        {
            // Best-effort dashboard widget — a failed summary/trend call
            // shouldn't block the Home page from showing its quick-access tiles.
        }
        finally
        {
            IsSummaryBusy = false;
        }
    }

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
                await Shell.Current.GoToAsync($"//{nameof(ResidentsPage)}");
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
            case "Finance":
                await Shell.Current.GoToAsync($"//{nameof(FinancePage)}");
                break;
            case "Staff":
                await Shell.Current.GoToAsync($"//{nameof(StaffPage)}");
                break;
            case "Services":
                await Shell.Current.GoToAsync($"//{nameof(ServicesPage)}");
                break;
            case "Complaints":
                await Shell.Current.GoToAsync($"//{nameof(ComplaintsPage)}");
                break;
            case "Committee":
                await Shell.Current.GoToAsync($"//{nameof(CommitteePage)}");
                break;
            case "Societies":
                await Shell.Current.GoToAsync($"//{nameof(SocietiesPage)}");
                break;
            case "Users":
                await Shell.Current.GoToAsync($"//{nameof(UsersPage)}");
                break;
            case "Roles & Permissions":
                await Shell.Current.GoToAsync($"//{nameof(RolesPage)}");
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
