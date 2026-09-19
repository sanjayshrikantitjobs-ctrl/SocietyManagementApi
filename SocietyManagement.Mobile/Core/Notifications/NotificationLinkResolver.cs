using System.Text.Json;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Core.Notifications;

/// <summary>Mirrors the web app's resolveNotificationLink (shared/models/
/// notification.model.ts) — maps a persisted notification's EventType to a
/// Shell route string ready for GoToAsync. Kept as the one place mapping
/// event names to destinations, same reasoning as the web version: update
/// here, not a second taxonomy, if a new event type is ever added.
///
/// Two events resolve to a page that doesn't exist yet on Mobile
/// (VisitorApproval* has no resident-facing approval/history screen in this
/// build) — those return null on purpose, same as the web resolver's own
/// `default: return null`, rather than guessing a destination.</summary>
public static class NotificationLinkResolver
{
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    public static string? Resolve(NotificationDto dto)
    {
        Dictionary<string, JsonElement>? payload = null;
        if (!string.IsNullOrWhiteSpace(dto.PayloadJson))
        {
            try
            {
                payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dto.PayloadJson!, CaseInsensitive);
            }
            catch
            {
                // Malformed/unexpected payload shape — fall back to a
                // link-less notification rather than throwing on open.
            }
        }

        return dto.EventType switch
        {
            "AnnouncementPublished" => TryGetInt(payload, "AnnouncementId") is { } announcementId
                ? $"{nameof(Features.Announcements.AnnouncementDetailPage)}?announcementId={announcementId}"
                : $"//{nameof(Features.Announcements.AnnouncementsListPage)}",

            // Raised/Reopened only ever reach Admin (ComplaintsPage's board);
            // Assigned/InProgress/Resolved only ever reach the complaint's own
            // raiser, whose own list is MyComplaintsPage — same split web's
            // resolver makes for the same reason.
            "ComplaintRaised" or "ComplaintReopened" => $"//{nameof(Features.Complaints.ComplaintsPage)}",
            "ComplaintAssigned" or "ComplaintInProgress" or "ComplaintResolved" => $"//{nameof(Features.Complaints.MyComplaintsPage)}",

            "FestivalContributionRecorded" or "FestivalExpenseApproved" => TryGetInt(payload, "festivalId") is { } festivalId
                ? $"{nameof(Features.Festivals.FestivalDetailPage)}?festivalId={festivalId}"
                : $"//{nameof(Features.Festivals.FestivalsListPage)}",

            // Created reaches SuperAdmin, Resolved reaches the ticket's own
            // creator — Mobile's SupportTicketsPage already serves both
            // audiences from one screen (see SupportTicketsViewModel), unlike
            // web's separate /admin/support-tickets vs /support routes.
            "SupportTicketCreated" or "SupportTicketResolved" => $"//{nameof(Features.Support.SupportTicketsPage)}",

            // No resident-facing visitor-approval or history screen exists on
            // Mobile yet — deliberately no destination rather than a guess.
            _ => null
        };
    }

    private static int? TryGetInt(Dictionary<string, JsonElement>? payload, string key)
    {
        if (payload is null || !payload.TryGetValue(key, out var value)) return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i) ? i : null;
    }
}
