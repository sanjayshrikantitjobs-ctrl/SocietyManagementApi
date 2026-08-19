using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.Application.Features.Dashboard;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    [HttpGet("admin-summary")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AdminSummary()
    {
        var result = await Mediator.Send(new GetAdminDashboardSummaryQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("member-summary")]
    public async Task<IActionResult> MemberSummary()
    {
        var result = await Mediator.Send(new GetMemberDashboardSummaryQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
