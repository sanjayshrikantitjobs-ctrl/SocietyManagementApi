using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Residents;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/vehicles")]
public class VehiclesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? memberId, [FromQuery] int? societyId, [FromQuery] string? search,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetVehiclesQuery(memberId, societyId, search, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Members.Create)]
    public async Task<IActionResult> Create(CreateVehicleCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { memberId = command.MemberId }, ApiResponse<int>.SuccessResponse(id, "Vehicle added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> Update(int id, UpdateVehicleCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Vehicle updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Members.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteVehicleCommand(id));
        return Ok(ApiResponse.SuccessResponse("Vehicle deleted."));
    }
}
