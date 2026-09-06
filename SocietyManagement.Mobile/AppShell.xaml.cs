using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Auth;
using SocietyManagement.Mobile.Features.Dashboard;
using SocietyManagement.Mobile.Features.Festivals;
using SocietyManagement.Mobile.Features.Festivals.Forms;
using SocietyManagement.Mobile.Features.Maintenance;
using SocietyManagement.Mobile.Features.Maintenance.Forms;
using SocietyManagement.Mobile.Features.Maintenance.Payments;
using SocietyManagement.Mobile.Features.Visitors.Forms;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;

    public AppShell(IAuthService authService, AuthState authState)
    {
        InitializeComponent();
        _authService = authService;
        Routing.RegisterRoute(nameof(ComingSoonPage), typeof(ComingSoonPage));
        Routing.RegisterRoute(nameof(ContactUsPage), typeof(ContactUsPage));
        // Reachable only via query-parameter navigation from FestivalsListPage
        // (a festival "detail push", not a flyout destination of its own).
        Routing.RegisterRoute(nameof(FestivalDetailPage), typeof(FestivalDetailPage));
        // Reachable only from TopBarView's user menu (Change Password) —
        // not a flyout destination of its own, same as the web app's own
        // avatar-dropdown-only /profile route.
        Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
        // Reachable only via query-parameter navigation from the Bills tab's
        // row menu (Maintenance module) — not a flyout destination.
        Routing.RegisterRoute(nameof(MaintenanceBillDetailPage), typeof(MaintenanceBillDetailPage));
        Routing.RegisterRoute(nameof(RecordPaymentPage), typeof(RecordPaymentPage));
        // Festival tab sub-screens — all reachable only via query-parameter
        // navigation from FestivalDetailPage's tabs, never flyout destinations.
        Routing.RegisterRoute(nameof(BudgetRevisionsPage), typeof(BudgetRevisionsPage));
        Routing.RegisterRoute(nameof(FlatContributionDetailPage), typeof(FlatContributionDetailPage));
        Routing.RegisterRoute(nameof(BudgetCategoryFormPage), typeof(BudgetCategoryFormPage));
        Routing.RegisterRoute(nameof(SponsorFormPage), typeof(SponsorFormPage));
        Routing.RegisterRoute(nameof(VendorFormPage), typeof(VendorFormPage));
        Routing.RegisterRoute(nameof(VolunteerFormPage), typeof(VolunteerFormPage));
        Routing.RegisterRoute(nameof(TaskFormPage), typeof(TaskFormPage));
        Routing.RegisterRoute(nameof(ExpenseFormPage), typeof(ExpenseFormPage));
        Routing.RegisterRoute(nameof(ContributionFormPage), typeof(ContributionFormPage));
        // Maintenance sub-tab forms — reachable only via query-parameter
        // navigation from MaintenancePage's tabs, never flyout destinations.
        Routing.RegisterRoute(nameof(SpecialChargeFormPage), typeof(SpecialChargeFormPage));
        Routing.RegisterRoute(nameof(FineFormPage), typeof(FineFormPage));
        Routing.RegisterRoute(nameof(WaterTankerLogFormPage), typeof(WaterTankerLogFormPage));
        Routing.RegisterRoute(nameof(GateFormPage), typeof(GateFormPage));
        Routing.RegisterRoute(nameof(PurposeFormPage), typeof(PurposeFormPage));
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

    /// <summary>Shared navigation target for a module that doesn't have a
    /// real screen yet, used by Home page's grid tiles (AppShell.xaml's own
    /// placeholder FlyoutItems navigate to their own dedicated route
    /// instead, and ComingSoonPage picks up its title from that flyout item
    /// directly — see ComingSoonPage.xaml.cs).</summary>
    public static async Task GoToComingSoonAsync(string title)
    {
        await Shell.Current.GoToAsync($"{nameof(ComingSoonPage)}?title={Uri.EscapeDataString(title)}");
    }
}
