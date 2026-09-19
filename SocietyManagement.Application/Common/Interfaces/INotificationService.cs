namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Abstraction over the SignalR hub (Infrastructure.Hubs.NotificationHub)
/// so Application handlers can push real-time events without depending on
/// Microsoft.AspNetCore.SignalR. Also the single place that persists a
/// Notification row per resolved recipient (see Infrastructure.Services.
/// NotificationService) — callers don't write notification rows themselves.
///
/// title/message are required, human-readable text for the persisted
/// inbox — every existing call site already computes these values for its
/// SignalR payload; this just also takes them as explicit parameters
/// instead of leaving them buried inside the anonymous payload object. The
/// SignalR payload/event itself is unchanged.
///
/// dedupeKey is optional: when a caller has a natural stable key for the
/// underlying domain event (e.g. "complaint-42-raised"), passing it lets
/// NotificationService silently skip re-inserting the same (user, event,
/// key) row if the same event is somehow raised twice (a retried command,
/// a duplicate delivery) — see NotificationConfiguration's unique index.
/// Left null, no dedupe is enforced (today's behavior).</summary>
public interface INotificationService
{
    Task SendToUserAsync(
        int userId, string eventName, object payload, string title, string message,
        string? dedupeKey = null, CancellationToken ct = default);

    /// <summary>societyId scopes which users get a *persisted* row (and,
    /// when supplied, also which users receive the live SignalR push — see
    /// implementation notes) to that one society's members with the given
    /// role. Omit only for a role that is intentionally cross-society
    /// (e.g. SendToRoleAsync(Roles.SuperAdmin, ...) for a new Support
    /// Ticket) — SuperAdmin has no SocietyId boundary, matching User.SocietyId's
    /// existing null-means-SuperAdmin convention.</summary>
    Task SendToRoleAsync(
        string roleName, string eventName, object payload, string title, string message,
        int? societyId = null, string? dedupeKey = null, CancellationToken ct = default);

    Task SendToAllAsync(
        string eventName, object payload, string title, string message,
        string? dedupeKey = null, CancellationToken ct = default);

    /// <summary>Every connection whose JWT carries this society_id — unlike
    /// SendToRoleAsync, which spans every society for that role unless a
    /// societyId is supplied, this is the one that always respects tenant
    /// isolation for society-scoped broadcasts (e.g. a published Announcement).</summary>
    Task SendToSocietyAsync(
        int societyId, string eventName, object payload, string title, string message,
        string? dedupeKey = null, CancellationToken ct = default);
}
