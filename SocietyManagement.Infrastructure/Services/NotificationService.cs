using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Infrastructure.Hubs;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>Live SignalR push (unchanged from before this feature) plus a
/// persisted Notification row per resolved recipient — the durable half of
/// the Notification Center. Every existing call site keeps calling these
/// same four methods; only the parameter list grew (title/message/dedupeKey),
/// and the live SignalR group targeting is byte-for-byte the same as before.</summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IApplicationDbContext _context;

    public NotificationService(IHubContext<NotificationHub> hubContext, IApplicationDbContext context)
    {
        _hubContext = hubContext;
        _context = context;
    }

    public async Task SendToUserAsync(
        int userId, string eventName, object payload, string title, string message,
        string? dedupeKey = null, CancellationToken ct = default)
    {
        await PersistAsync(new[] { userId }, eventName, payload, title, message, dedupeKey, ct);
        await _hubContext.Clients.Group($"user-{userId}").SendAsync(eventName, payload, ct);
    }

    public async Task SendToRoleAsync(
        string roleName, string eventName, object payload, string title, string message,
        int? societyId = null, string? dedupeKey = null, CancellationToken ct = default)
    {
        // societyId only narrows who gets a *persisted* row — the live
        // SignalR group is unchanged (still every "role-{roleName}"
        // connection) so no currently-connected, legitimately-subscribed
        // client stops receiving the real-time push.
        var recipientIds = await _context.Users
            .Where(u => u.Role.Name == roleName && (societyId == null || u.SocietyId == societyId))
            .Select(u => u.Id)
            .ToListAsync(ct);

        await PersistAsync(recipientIds, eventName, payload, title, message, dedupeKey, ct);
        await _hubContext.Clients.Group($"role-{roleName}").SendAsync(eventName, payload, ct);
    }

    public async Task SendToAllAsync(
        string eventName, object payload, string title, string message,
        string? dedupeKey = null, CancellationToken ct = default)
    {
        var recipientIds = await _context.Users.Select(u => u.Id).ToListAsync(ct);

        await PersistAsync(recipientIds, eventName, payload, title, message, dedupeKey, ct);
        await _hubContext.Clients.All.SendAsync(eventName, payload, ct);
    }

    public async Task SendToSocietyAsync(
        int societyId, string eventName, object payload, string title, string message,
        string? dedupeKey = null, CancellationToken ct = default)
    {
        var recipientIds = await _context.Users
            .Where(u => u.SocietyId == societyId)
            .Select(u => u.Id)
            .ToListAsync(ct);

        await PersistAsync(recipientIds, eventName, payload, title, message, dedupeKey, ct);
        await _hubContext.Clients.Group($"society-{societyId}").SendAsync(eventName, payload, ct);
    }

    private async Task PersistAsync(
        IReadOnlyCollection<int> recipientUserIds, string eventName, object payload, string title, string message,
        string? dedupeKey, CancellationToken ct)
    {
        if (recipientUserIds.Count == 0) return;

        var toInsert = recipientUserIds.AsEnumerable();

        // Only worth the lookup when a caller actually supplied a dedupe
        // key — otherwise every insert is allowed through, matching
        // "no dedupe enforced" for call sites with no natural stable key.
        if (dedupeKey is not null)
        {
            var alreadyNotified = await _context.Notifications
                .Where(n => n.EventType == eventName && n.DedupeKey == dedupeKey && recipientUserIds.Contains(n.UserId))
                .Select(n => n.UserId)
                .ToListAsync(ct);
            toInsert = recipientUserIds.Except(alreadyNotified);
        }

        // Check-then-insert, not fully race-proof under concurrent duplicate
        // delivery of the exact same event within the same instant — the
        // unique index on (UserId, EventType, DedupeKey) is the hard
        // backstop if that ever happens, at the cost of the batch's
        // SaveChanges throwing rather than silently continuing. Accepted
        // tradeoff: callers raise each event once per request already, so
        // this is a genuinely rare race, not a routine path.
        var payloadJson = JsonSerializer.Serialize(payload);
        var now = DateTime.UtcNow;

        foreach (var userId in toInsert)
        {
            await _context.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                EventType = eventName,
                Title = title,
                Message = message,
                PayloadJson = payloadJson,
                CreatedAt = now,
                DedupeKey = dedupeKey
            }, ct);
        }

        await _context.SaveChangesAsync(ct);
    }
}
