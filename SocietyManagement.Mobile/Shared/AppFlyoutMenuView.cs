using Microsoft.Maui.Controls.Shapes;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Shared;

/// <summary>Mirrors main-layout.component.ts's NAV_ITEMS/navNodes() grouping
/// exactly — same groups, same per-item visibility flags, same "auto-expand
/// whichever group contains the active route, otherwise collapsed" rule.
/// Built imperatively (not XAML + bindings) because MAUI Shell's flyout has
/// no built-in collapsible-group concept, and a heterogeneous leaf/group
/// list is far simpler to get right as plain code than via a
/// DataTemplateSelector; the underlying FlyoutItem/ShellContent/Tab tree in
/// AppShell.xaml is untouched (routes, tab bars, deep links, ComingSoonPage's
/// title fallback all keep working) — this view only replaces what's
/// physically drawn in the flyout via Shell.FlyoutContent.</summary>
public class AppFlyoutMenuView : ScrollView
{
    private sealed record NavItem(
        string Title, string Route, string? Group = null,
        bool AdminOnly = false, bool SuperAdminOnly = false, bool MemberOnly = false,
        bool HideForWatchman = false, Func<AuthState, bool>? CustomVisibility = null,
        string[]? AlsoMatchRoutes = null)
    {
        public bool IsVisibleFor(AuthState auth) =>
            (!AdminOnly || auth.IsAdmin) &&
            (!SuperAdminOnly || auth.IsSuperAdmin) &&
            (!MemberOnly || auth.IsMember) &&
            (!HideForWatchman || !auth.IsWatchman) &&
            (CustomVisibility?.Invoke(auth) ?? true);

        // Visitors/Vehicle Security are FlyoutItems with several internal
        // Tabs (see AppShell.xaml) — landing on a sibling tab still counts
        // as "on this item" so the group stays expanded/highlighted.
        public bool MatchesRoute(string? route) =>
            route is not null && (route == Route || (AlsoMatchRoutes?.Contains(route) ?? false));
    }

    // Same routes as the existing FlyoutItems in AppShell.xaml, just
    // reorganized into the web sidebar's groups — see main-layout.component.ts.
    private static readonly List<NavItem> Items = new()
    {
        new("🏠 Home", "DashboardPage", HideForWatchman: true),

        new("📢 Announcements", "AnnouncementsListPage", Group: "💬 Community"),
        new("🎉 Festivals & Events", "FestivalsListPage", Group: "💬 Community", HideForWatchman: true),
        new("🏛️ Committee", "CommitteePage", Group: "💬 Community", HideForWatchman: true),
        new("📢 Complaints", "ComplaintsPage", Group: "💬 Community", AdminOnly: true),

        new("🏛️ Facilities", "FacilitiesListPage", Group: "🏛️ Facilities & Bookings", HideForWatchman: true),

        new("🪑 Assets", "AssetsListPage", Group: "📦 Assets & Rentals", HideForWatchman: true),

        // Visitors/Vehicle Security each still open into their own Tab-bar
        // page (New Visitor/Currently Inside/History/Gates/... etc.) exactly
        // as before — only the flyout entry point is now grouped under Security.
        new("🚶 Visitors", "NewVisitorPage", Group: "🛡️ Security",
            AlsoMatchRoutes: new[] { "CurrentlyInsidePage", "VisitorHistoryPage", "GatesPage", "PurposesPage", "VisitorSettingsPage" }),
        new("🚗 Vehicle Security", "VehicleScanPage", Group: "🛡️ Security",
            AlsoMatchRoutes: new[] { "VehicleScanHistoryPage", "ParkingFinesPage" }),

        new("🛠️ Maintenance", "MaintenancePage", Group: "🏢 Society Management", AdminOnly: true),
        new("👥 Residents", "ResidentsPage", Group: "🏢 Society Management", AdminOnly: true),
        new("👷 Staff", "StaffPage", Group: "🏢 Society Management", AdminOnly: true),
        new("🧰 Services", "ServicesPage", Group: "🏢 Society Management", AdminOnly: true),

        new("💰 Finance", "FinancePage", AdminOnly: true),

        new("🧾 My Bills", "MyBillsPage", Group: "🏘️ My Society", MemberOnly: true),
        new("💬 My Complaints", "MyComplaintsPage", Group: "🏘️ My Society", MemberOnly: true),
        new("🏛️ My Bookings", "MyFacilityBookingsPage", Group: "🏘️ My Society", MemberOnly: true),
        new("🪑 My Rentals", "MyAssetRentalsPage", Group: "🏘️ My Society", MemberOnly: true),
        new("👨‍👩‍👧 My Family", "MyFamilyPage", Group: "🏘️ My Society", MemberOnly: true),
        new("❓ Help & Support", "HelpAndSupportPage", Group: "🏘️ My Society", CustomVisibility: a => a.ShowHelpAndSupport),

        new("🏢 Societies", "SocietiesPage", Group: "🔐 Administration", AdminOnly: true),
        new("👤 Users", "UsersPage", Group: "🔐 Administration", AdminOnly: true),
        new("🔐 Roles & Permissions", "RolesPage", Group: "🔐 Administration", AdminOnly: true),
        new("🎫 Support Tickets", "SupportTicketsPage", Group: "🔐 Administration", SuperAdminOnly: true)
    };

    private readonly AuthState _auth;
    private readonly HashSet<string> _expandedGroups = new();
    private readonly VerticalStackLayout _root = new() { Spacing = 2, Padding = new Thickness(8, 12) };
    private string? _activeRoute;

    public AppFlyoutMenuView(AuthState auth)
    {
        _auth = auth;
        Content = _root;
        _auth.PropertyChanged += (_, _) => Rebuild();
        Rebuild();
    }

    /// <summary>Called from AppShell's OnNavigated override on every
    /// navigation (including the first, so a cold start into a deep link
    /// opens the right group already expanded) — mirrors main-layout.
    /// component.ts's expandToActiveGroup, add-only: a group the user
    /// expanded themselves never auto-collapses just from browsing elsewhere.</summary>
    public void NotifyNavigated(string route)
    {
        _activeRoute = route;
        var item = Items.FirstOrDefault(i => i.MatchesRoute(route));
        if (item?.Group is not null) _expandedGroups.Add(item.Group);
        Rebuild();
    }

    private void Rebuild()
    {
        _root.Children.Clear();
        string? currentGroup = null;
        VerticalStackLayout? childContainer = null;

        foreach (var item in Items.Where(i => i.IsVisibleFor(_auth)))
        {
            if (item.Group is null)
            {
                currentGroup = null;
                _root.Children.Add(BuildLeaf(item, indent: false));
                continue;
            }

            if (currentGroup != item.Group)
            {
                currentGroup = item.Group;
                var containsActive = Items.Any(i => i.Group == item.Group && i.MatchesRoute(_activeRoute));
                var expanded = containsActive || _expandedGroups.Contains(item.Group);
                childContainer = new VerticalStackLayout { Spacing = 2, IsVisible = expanded };
                _root.Children.Add(BuildGroupHeader(item.Group, childContainer, containsActive, expanded));
                _root.Children.Add(childContainer);
            }

            childContainer!.Children.Add(BuildLeaf(item, indent: true));
        }
    }

    private View BuildGroupHeader(string title, VerticalStackLayout childContainer, bool isActive, bool startsExpanded)
    {
        var res = Application.Current!.Resources;

        var label = new Label
        {
            Text = title, FontSize = 14, VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.StartAndExpand,
            TextColor = isActive ? (Color)res["Primary"] : (Color)res["TextMuted"],
            FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None
        };
        var chevron = new Label
        {
            Text = "›", FontSize = 20, VerticalOptions = LayoutOptions.Center,
            TextColor = (Color)res["TextMuted"], Rotation = startsExpanded ? 90 : 0
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) },
            Padding = new Thickness(14, 11), HeightRequest = 46
        };
        row.Add(label, 0);
        row.Add(chevron, 1);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            var nowExpanded = !childContainer.IsVisible;
            childContainer.IsVisible = nowExpanded;
            if (nowExpanded) _expandedGroups.Add(title); else _expandedGroups.Remove(title);
            chevron.RotateTo(nowExpanded ? 90 : 0, 120);
        };
        row.GestureRecognizers.Add(tap);
        return row;
    }

    private View BuildLeaf(NavItem item, bool indent)
    {
        var res = Application.Current!.Resources;
        var isActive = item.MatchesRoute(_activeRoute);

        var label = new Label
        {
            Text = item.Title, FontSize = 14, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = isActive ? (Color)res["PrimaryDarkText"] : (Color)res["TextMuted"],
            FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None
        };
        var border = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(indent ? 28 : 14, 11),
            Margin = new Thickness(indent ? 8 : 0, 0, 0, 0),
            BackgroundColor = isActive ? (Color)res["PrimaryLight"] : Colors.Transparent,
            Content = label
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await NavigateAsync(item.Route);
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private static async Task NavigateAsync(string route)
    {
        if (Shell.Current is null) return;
        Shell.Current.FlyoutIsPresented = false;
        await Shell.Current.GoToAsync($"//{route}");
    }
}
