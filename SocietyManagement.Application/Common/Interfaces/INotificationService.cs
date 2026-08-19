namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Abstraction over the SignalR hub (Infrastructure.Hubs.NotificationHub)
/// so Application handlers can push real-time events without depending on
/// Microsoft.AspNetCore.SignalR.</summary>
public interface INotificationService
{
    Task SendToUserAsync(int userId, string eventName, object payload, CancellationToken ct = default);
    Task SendToRoleAsync(string roleName, string eventName, object payload, CancellationToken ct = default);
    Task SendToAllAsync(string eventName, object payload, CancellationToken ct = default);
}
