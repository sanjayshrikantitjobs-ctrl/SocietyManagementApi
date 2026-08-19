using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Permissions.Queries;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class PermissionsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Roles_.View)]
    public async Task<IActionResult> GetAll()
    {
        var result = await Mediator.Send(new GetAllPermissionsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
