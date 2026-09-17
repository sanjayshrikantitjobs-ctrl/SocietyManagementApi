using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Announcements;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Reuses the pre-seeded Notices.* permission group (View/Manage)
/// rather than adding a parallel Announcements.* group — Notices was seeded
/// specifically for this module ahead of time, see RoleConstants.cs.</summary>
[Authorize]
[Route("api/announcements")]
public class AnnouncementsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Notices.Manage)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AnnouncementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] AnnouncementStatus? status, [FromQuery] AnnouncementType? type,
        [FromQuery] string? search, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetAnnouncementsQuery(societyId, status, type, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("published")]
    [HasPermission(Permissions.Notices.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AnnouncementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublished(
        [FromQuery] int societyId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetPublishedAnnouncementsQuery(societyId, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("unread-count")]
    [HasPermission(Permissions.Notices.View)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetUnreadAnnouncementCountQuery(societyId));
        return Ok(ApiResponse<int>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Notices.View)]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetAnnouncementByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Notices.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateAnnouncementCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Announcement created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Notices.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateAnnouncementCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Announcement updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Notices.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteAnnouncementCommand(id));
        return Ok(ApiResponse.SuccessResponse("Announcement deleted."));
    }

    [HttpPost("{id:int}/publish")]
    [HasPermission(Permissions.Notices.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(int id)
    {
        await Mediator.Send(new PublishAnnouncementCommand(id));
        return Ok(ApiResponse.SuccessResponse("Announcement published."));
    }

    [HttpPost("{id:int}/mark-read")]
    [HasPermission(Permissions.Notices.View)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(int id)
    {
        await Mediator.Send(new MarkAnnouncementReadCommand(id));
        return Ok(ApiResponse.SuccessResponse("Marked as read."));
    }

    [HttpPost("{id:int}/toggle-saved")]
    [HasPermission(Permissions.Notices.View)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ToggleSaved(int id)
    {
        var isSaved = await Mediator.Send(new ToggleAnnouncementSavedCommand(id));
        return Ok(ApiResponse<bool>.SuccessResponse(isSaved, isSaved ? "Saved." : "Removed from saved."));
    }
}
