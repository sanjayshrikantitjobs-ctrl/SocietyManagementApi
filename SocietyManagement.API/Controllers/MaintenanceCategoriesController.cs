using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/maintenance-categories")]
public class MaintenanceCategoriesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetMaintenanceCategoriesQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Create(CreateMaintenanceCategoryCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { societyId = command.SocietyId }, ApiResponse<int>.SuccessResponse(id, "Category created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Update(int id, UpdateMaintenanceCategoryCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Category updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteMaintenanceCategoryCommand(id));
        return Ok(ApiResponse.SuccessResponse("Category deleted."));
    }
}
