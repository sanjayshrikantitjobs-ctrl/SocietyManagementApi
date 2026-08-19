using Microsoft.AspNetCore.SignalR;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Infrastructure.Hubs;

namespace SocietyManagement.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(IHubContext<NotificationHub> hubContext) => _hubContext = hubContext;

    public Task SendToUserAsync(int userId, string eventName, object payload, CancellationToken ct = default) =>
        _hubContext.Clients.Group($"user-{userId}").SendAsync(eventName, payload, ct);

    public Task SendToRoleAsync(string roleName, string eventName, object payload, CancellationToken ct = default) =>
        _hubContext.Clients.Group($"role-{roleName}").SendAsync(eventName, payload, ct);

    public Task SendToAllAsync(string eventName, object payload, CancellationToken ct = default) =>
        _hubContext.Clients.All.SendAsync(eventName, payload, ct);
}
