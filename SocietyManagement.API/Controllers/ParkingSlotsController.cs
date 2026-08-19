using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.ParkingSlots;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class ParkingSlotsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId, [FromQuery] ParkingStatus? status)
    {
        var result = await Mediator.Send(new GetParkingSlotsQuery(societyId, status));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Create(CreateParkingSlotCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Parking slot created."));
    }

    [HttpPost("{id:int}/allocate")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Allocate(int id, [FromQuery] int? flatId)
    {
        await Mediator.Send(new AllocateParkingSlotCommand(id, flatId));
        return Ok(ApiResponse.SuccessResponse(flatId.HasValue ? "Parking slot allocated." : "Parking slot vacated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteParkingSlotCommand(id));
        return Ok(ApiResponse.SuccessResponse("Parking slot deleted."));
    }
}
