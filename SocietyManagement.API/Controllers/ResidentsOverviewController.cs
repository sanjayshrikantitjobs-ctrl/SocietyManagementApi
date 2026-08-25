using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Occupancy;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/residents-overview")]
public class ResidentsOverviewController : ApiControllerBase
{
    [HttpGet("summary")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetSummary([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetResidentsOverviewSummaryQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("recent-changes")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetRecentChanges([FromQuery] int societyId, [FromQuery] int take = 10)
    {
        var result = await Mediator.Send(new GetRecentOccupancyChangesQuery(societyId, take));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
