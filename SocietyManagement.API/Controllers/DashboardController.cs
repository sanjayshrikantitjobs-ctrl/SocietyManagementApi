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
    [Authorize(Roles = Roles.Admin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminSummary([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetAdminDashboardSummaryQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("monthly-collection-trend")]
    [Authorize(Roles = Roles.Admin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<List<AdminMonthlyCollectionPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyCollectionTrend([FromQuery] int societyId, [FromQuery] int months = 6)
    {
        var result = await Mediator.Send(new GetMonthlyCollectionTrendQuery(societyId, months));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("upcoming")]
    [Authorize(Roles = Roles.Admin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<UpcomingItemsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upcoming([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetUpcomingItemsQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("recent-activity")]
    [Authorize(Roles = Roles.Admin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<List<RecentActivityItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecentActivity([FromQuery] int societyId, [FromQuery] int take = 10)
    {
        var result = await Mediator.Send(new GetRecentActivityQuery(societyId, take));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("member-summary")]
    [ProducesResponseType(typeof(ApiResponse<MemberDashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MemberSummary()
    {
        var result = await Mediator.Send(new GetMemberDashboardSummaryQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
