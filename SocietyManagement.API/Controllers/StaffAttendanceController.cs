using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Attendance;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

// Reuses the existing staff.view / staff.manage permissions — attendance is
// part of managing staff, not a separate access tier.
[Authorize]
[Route("api/staff-attendance")]
public class StaffAttendanceController : ApiControllerBase
{
    [HttpGet("daily")]
    [HasPermission(Permissions.Staff.View)]
    [ProducesResponseType(typeof(ApiResponse<List<DailyAttendanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDaily([FromQuery] int societyId, [FromQuery] DateTime date)
    {
        var result = await Mediator.Send(new GetDailyAttendanceQuery(societyId, date));
        return Ok(ApiResponse<List<DailyAttendanceDto>>.SuccessResponse(result));
    }

    [HttpGet("monthly")]
    [HasPermission(Permissions.Staff.View)]
    [ProducesResponseType(typeof(ApiResponse<MonthlyAttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthly([FromQuery] int societyId, [FromQuery] int year, [FromQuery] int month)
    {
        var result = await Mediator.Send(new GetMonthlyAttendanceQuery(societyId, year, month));
        return Ok(ApiResponse<MonthlyAttendanceDto>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Staff.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mark(MarkAttendanceCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Attendance saved."));
    }
}
