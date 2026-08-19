using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/special-charges")]
public class SpecialChargesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId, [FromQuery] int? flatId)
    {
        var result = await Mediator.Send(new GetSpecialChargesQuery(societyId, flatId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Create(CreateSpecialChargeCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { }, ApiResponse<int>.SuccessResponse(id, "Special charge added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Update(int id, UpdateSpecialChargeCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Special charge updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteSpecialChargeCommand(id));
        return Ok(ApiResponse.SuccessResponse("Special charge deleted."));
    }
}
