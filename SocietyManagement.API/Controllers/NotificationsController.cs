using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.Application.Features.Notifications;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>No [HasPermission] anywhere here, deliberately — a notification
/// inbox is inherently self-scoped (every handler resolves the caller's own
/// UserId via ICurrentUserService and nothing else), same as "My Complaints"/
/// "My Bills" elsewhere in this app. [Authorize] is the only gate a personal
/// inbox needs.</summary>
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool unreadOnly = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetMyNotificationsQuery(unreadOnly, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await Mediator.Send(new GetUnreadNotificationCountQuery());
        return Ok(ApiResponse<int>.SuccessResponse(result));
    }

    [HttpPost("{id:int}/mark-read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(int id)
    {
        await Mediator.Send(new MarkNotificationReadCommand(id));
        return Ok(ApiResponse.SuccessResponse("Marked as read."));
    }

    [HttpPost("mark-all-read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead()
    {
        await Mediator.Send(new MarkAllNotificationsReadCommand());
        return Ok(ApiResponse.SuccessResponse("All notifications marked as read."));
    }
}
