using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/water-tanker-logs")]
public class WaterTankerLogsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] DateTime month, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetWaterTankerLogsQuery(societyId, month, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("summary")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetSummary([FromQuery] int societyId, [FromQuery] DateTime month)
    {
        var result = await Mediator.Send(new GetWaterTankerLogSummaryQuery(societyId, month));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Create(CreateWaterTankerLogCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Water tanker entry logged."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Update(int id, UpdateWaterTankerLogCommand command)
    {
        if (id != command.Id) return BadRequest();
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Entry updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteWaterTankerLogCommand(id));
        return Ok(ApiResponse.SuccessResponse("Entry deleted."));
    }
}
