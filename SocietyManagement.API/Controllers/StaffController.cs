using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Staff;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class StaffController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Staff.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] StaffCategory? category,
        [FromQuery] bool? isActive, [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetStaffQuery(societyId, search, category, isActive, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Staff.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetStaffByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<IActionResult> Create(CreateStaffCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Staff member added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<IActionResult> Update(int id, UpdateStaffCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id and body id must match."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Staff member updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteStaffCommand(id));
        return Ok(ApiResponse.SuccessResponse("Staff member deleted."));
    }
}
