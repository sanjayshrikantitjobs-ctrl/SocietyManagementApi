using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Events;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/events")]
public class EventsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Events.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] EventStatus? status, [FromQuery] int? festivalId,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetEventsQuery(societyId, status, festivalId, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Events.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetEventByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/capacity-summary")]
    [HasPermission(Permissions.Events.View)]
    public async Task<IActionResult> GetCapacitySummary(int id)
    {
        var result = await Mediator.Send(new GetEventCapacitySummaryQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> Create(CreateEventCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Event created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> Update(int id, UpdateEventCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Event updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteEventCommand(id));
        return Ok(ApiResponse.SuccessResponse("Event deleted."));
    }

    [HttpPost("{id:int}/open")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> Open(int id)
    {
        await Mediator.Send(new OpenEventCommand(id));
        return Ok(ApiResponse.SuccessResponse("Event opened for RSVPs."));
    }

    [HttpPost("{id:int}/close")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> Close(int id)
    {
        await Mediator.Send(new CloseEventCommand(id));
        return Ok(ApiResponse.SuccessResponse("Event closed to new RSVPs."));
    }

    [HttpPost("{id:int}/complete")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> Complete(int id)
    {
        await Mediator.Send(new CompleteEventCommand(id));
        return Ok(ApiResponse.SuccessResponse("Event marked completed."));
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.Events.Manage)]
    public async Task<IActionResult> Cancel(int id)
    {
        await Mediator.Send(new CancelEventCommand(id));
        return Ok(ApiResponse.SuccessResponse("Event cancelled."));
    }
}
