using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Occupancy;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/occupancy-settings")]
public class OccupancySettingsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> Get([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetOccupancySettingsQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPut]
    [HasPermission(Permissions.Occupancy.ManageSettings)]
    public async Task<IActionResult> Update(UpdateOccupancySettingsCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Occupancy settings updated."));
    }
}
