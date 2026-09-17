using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Facilities;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/facilities")]
public class FacilitiesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Facilities.View)]
    [ProducesResponseType(typeof(ApiResponse<List<FacilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId, [FromQuery] bool activeOnly = false)
    {
        var result = await Mediator.Send(new GetFacilitiesQuery(societyId, activeOnly));
        return Ok(ApiResponse<List<FacilityDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Facilities.View)]
    [ProducesResponseType(typeof(ApiResponse<FacilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetFacilityByIdQuery(id));
        return Ok(ApiResponse<FacilityDto>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/availability")]
    [HasPermission(Permissions.Facilities.View)]
    [ProducesResponseType(typeof(ApiResponse<List<FacilitySlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailability(int id, [FromQuery] DateTime date)
    {
        var result = await Mediator.Send(new GetFacilityAvailabilityQuery(id, date));
        return Ok(ApiResponse<List<FacilitySlotDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/blackout-dates")]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse<List<FacilityBlackoutDateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlackoutDates(int id)
    {
        var result = await Mediator.Send(new GetFacilityBlackoutDatesQuery(id));
        return Ok(ApiResponse<List<FacilityBlackoutDateDto>>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateFacilityCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Facility created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateFacilityCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Facility updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteFacilityCommand(id));
        return Ok(ApiResponse.SuccessResponse("Facility deleted."));
    }

    [HttpPost("{id:int}/blackout-dates")]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddBlackoutDate(int id, [FromBody] AddBlackoutDateRequest request)
    {
        var blackoutId = await Mediator.Send(new AddFacilityBlackoutDateCommand(id, request.BlackoutDate, request.Reason));
        return Ok(ApiResponse<int>.SuccessResponse(blackoutId, "Blackout date added."));
    }

    [HttpDelete("blackout-dates/{blackoutId:int}")]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveBlackoutDate(int blackoutId)
    {
        await Mediator.Send(new RemoveFacilityBlackoutDateCommand(blackoutId));
        return Ok(ApiResponse.SuccessResponse("Blackout date removed."));
    }

    public record AddBlackoutDateRequest(DateTime BlackoutDate, string? Reason);
}
