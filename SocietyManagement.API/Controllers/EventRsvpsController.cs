using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Events;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/event-rsvps")]
public class EventRsvpsController : ApiControllerBase
{
    [HttpGet("mine")]
    [HasPermission(Permissions.Events.View)]
    public async Task<IActionResult> GetMine([FromQuery] int eventId)
    {
        var result = await Mediator.Send(new GetMyRsvpQuery(eventId));
        return Ok(ApiResponse<object?>.SuccessResponse(result));
    }

    [HttpGet]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> GetForEvent([FromQuery] int eventId)
    {
        var result = await Mediator.Send(new GetRsvpsForEventQuery(eventId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Events.Rsvp)]
    public async Task<IActionResult> CreateOrUpdate(CreateOrUpdateRsvpCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<object>.SuccessResponse(result, "RSVP saved."));
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.Events.Rsvp)]
    public async Task<IActionResult> Cancel(int id)
    {
        await Mediator.Send(new CancelRsvpCommand(id));
        return Ok(ApiResponse.SuccessResponse("RSVP cancelled."));
    }

    [HttpPost("check-in")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> CheckIn(CheckInCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Checked in."));
    }

    [HttpGet("by-token/{qrToken}")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> GetByToken(string qrToken)
    {
        var result = await Mediator.Send(new GetRsvpByTokenQuery(qrToken));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
