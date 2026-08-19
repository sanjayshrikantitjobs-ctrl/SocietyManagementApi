using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Visitors;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/visitors")]
public class VisitorsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Visitors.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetVisitorsQuery(societyId, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
