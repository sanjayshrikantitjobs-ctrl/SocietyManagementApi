using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/maintenance-settings")]
public class MaintenanceSettingsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> Get([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetMaintenanceSettingsQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPut]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Upsert(UpsertMaintenanceSettingsCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Settings saved."));
    }
}
