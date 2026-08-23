using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Users.Commands.CreateUser;
using SocietyManagement.Application.Features.Users.Commands.DeleteUser;
using SocietyManagement.Application.Features.Users.Commands.LockUser;
using SocietyManagement.Application.Features.Users.Commands.ResetUserPassword;
using SocietyManagement.Application.Features.Users.Commands.UpdateUser;
using SocietyManagement.Application.Features.Users.Queries.GetUserById;
using SocietyManagement.Application.Features.Users.Queries.GetUsers;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>User Management module — spec: "Admin can Create User, Assign Role,
/// Reset Password, Lock Account." All actions require Users.* permissions.</summary>
[Authorize]
public class UsersController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search, [FromQuery] int? roleId, [FromQuery] bool? isActive,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetUsersQuery(search, roleId, isActive, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetUserByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Create)]
    public async Task<IActionResult> Create(CreateUserCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "User created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> Update(int id, UpdateUserCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("User updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Users.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteUserCommand(id));
        return Ok(ApiResponse.SuccessResponse("User deleted."));
    }

    [HttpPost("{id:int}/lock")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> Lock(int id, [FromQuery] bool locked = true)
    {
        await Mediator.Send(new LockUserCommand(id, locked));
        return Ok(ApiResponse.SuccessResponse(locked ? "User locked." : "User unlocked."));
    }

    [HttpPost("{id:int}/reset-password")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> ResetPassword(int id)
    {
        await Mediator.Send(new ResetUserPasswordCommand(id));
        return Ok(ApiResponse.SuccessResponse("Temporary password emailed to the user."));
    }
}
