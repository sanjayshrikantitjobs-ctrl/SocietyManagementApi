using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Visitors;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/visitor-settings")]
public class VisitorSettingsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Visitors.Manage)]
    public async Task<IActionResult> Get([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetVisitorSettingsQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPut]
    [HasPermission(Permissions.Visitors.Manage)]
    public async Task<IActionResult> Upsert(UpsertVisitorSettingsCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Visitor settings updated."));
    }
}
