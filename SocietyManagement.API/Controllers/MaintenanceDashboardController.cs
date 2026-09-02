using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/maintenance")]
public class MaintenanceDashboardController : ApiControllerBase
{
    [HttpGet("dashboard")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetDashboard([FromQuery] int societyId, [FromQuery] DateTime? month)
    {
        var result = await Mediator.Send(new GetMaintenanceDashboardQuery(societyId, month));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
