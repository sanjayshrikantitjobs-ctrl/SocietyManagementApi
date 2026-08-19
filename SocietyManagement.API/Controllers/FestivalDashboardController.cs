using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/festival-dashboard")]
public class FestivalDashboardController : ApiControllerBase
{
    [HttpGet("{festivalId:int}")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetSummary(int festivalId)
    {
        var result = await Mediator.Send(new GetFestivalDashboardQuery(festivalId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
