using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Buildings;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class BuildingsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetBuildingsQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetBuildingByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Create(CreateBuildingCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Building created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Update(int id, UpdateBuildingCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Building updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteBuildingCommand(id));
        return Ok(ApiResponse.SuccessResponse("Building deleted."));
    }
}
