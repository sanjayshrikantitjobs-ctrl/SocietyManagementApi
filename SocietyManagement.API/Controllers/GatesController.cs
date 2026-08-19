using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Visitors;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/gates")]
public class GatesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Visitors.View)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId, [FromQuery] bool? isActive)
    {
        var result = await Mediator.Send(new GetGatesQuery(societyId, isActive));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Visitors.ManageGates)]
    public async Task<IActionResult> Create(CreateGateCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), ApiResponse<int>.SuccessResponse(id, "Gate added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Visitors.ManageGates)]
    public async Task<IActionResult> Update(int id, UpdateGateCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Gate updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Visitors.ManageGates)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteGateCommand(id));
        return Ok(ApiResponse.SuccessResponse("Gate deleted."));
    }
}
