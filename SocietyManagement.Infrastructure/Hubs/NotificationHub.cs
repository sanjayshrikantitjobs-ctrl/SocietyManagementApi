using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SocietyManagement.Infrastructure.Hubs;

/// <summary>
/// Real-time channel per spec ("Real-time: Use SignalR" — new notice, complaint
/// update, payment success, festival reminder, ...). Clients connect to
/// /hubs/notifications with the JWT access token as a query-string bearer
/// (see API/Program.cs OnMessageReceived) and are auto-joined to a per-user
/// group ("user-{id}"), a per-role group ("role-{RoleName}"), and — for
/// anyone scoped to one society (everyone except Super Admin, who carries no
/// society_id claim) — a per-society group ("society-{id}") so
/// INotificationService can target any of the three.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var societyId = Context.User?.FindFirst("society_id")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        if (!string.IsNullOrEmpty(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role-{role}");
        }
        if (!string.IsNullOrEmpty(societyId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"society-{societyId}");
        }

        await base.OnConnectedAsync();
    }
}
