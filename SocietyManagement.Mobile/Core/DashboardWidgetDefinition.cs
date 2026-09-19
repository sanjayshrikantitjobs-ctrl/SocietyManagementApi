using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Core;

public enum WidgetSize { Small, Medium, Large }

/// <summary>Registry-entry contract for a dashboard widget (§6/§12 of the
/// strategy doc) — mirrors Web's DashboardWidgetDef field-for-field so both
/// platforms can describe the same widget catalog. Nothing renders from
/// this yet; Step 6 wires an actual widget host into DashboardPage.</summary>
public record DashboardWidgetDefinition(
    string Id, string Title, string Icon,
    string? RequiredPermission = null, string[]? Roles = null, WidgetSize Size = WidgetSize.Medium);

/// <summary>Filters a widget catalog down to what the signed-in user may
/// see — reuses AuthState.HasPermission()/RoleName exactly as every other
/// permission check in this app, rather than a parallel authorization path.</summary>
public static class DashboardWidgetRegistry
{
    public static IEnumerable<DashboardWidgetDefinition> FilterVisible(
        IEnumerable<DashboardWidgetDefinition> defs, AuthState auth)
    {
        foreach (var d in defs)
        {
            if (d.RequiredPermission is not null && !auth.HasPermission(d.RequiredPermission)) continue;
            if (d.Roles is { Length: > 0 } && !d.Roles.Contains(auth.RoleName ?? string.Empty)) continue;
            yield return d;
        }
    }
}
