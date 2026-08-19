using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Floors;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class FloorsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetAll([FromQuery] int wingId)
    {
        var result = await Mediator.Send(new GetFloorsQuery(wingId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetFloorByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Create(CreateFloorCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Floor created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Update(int id, UpdateFloorCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Floor updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteFloorCommand(id));
        return Ok(ApiResponse.SuccessResponse("Floor deleted."));
    }
}
