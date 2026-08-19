using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Roles.Commands;
using SocietyManagement.Application.Features.Roles.Queries;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Dynamic role + permission management — spec: "Create dynamic
/// permission management... Permissions should be stored in database."</summary>
[Authorize]
public class RolesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Roles_.View)]
    public async Task<IActionResult> GetRoles()
    {
        var result = await Mediator.Send(new GetRolesQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Roles_.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetRoleByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Roles_.Manage)]
    public async Task<IActionResult> Create(CreateRoleCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Role created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Roles_.Manage)]
    public async Task<IActionResult> Update(int id, UpdateRoleCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Role updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Roles_.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteRoleCommand(id));
        return Ok(ApiResponse.SuccessResponse("Role deleted."));
    }
}
