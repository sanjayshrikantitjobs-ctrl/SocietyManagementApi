using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/fine-records")]
public class FineRecordsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId, [FromQuery] int? flatId, [FromQuery] FineStatus? status)
    {
        var result = await Mediator.Send(new GetFinesQuery(societyId, flatId, status));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Create(CreateFineRecordCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { }, ApiResponse<int>.SuccessResponse(id, "Fine recorded."));
    }

    [HttpPost("{id:int}/waive")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Waive(int id)
    {
        await Mediator.Send(new WaiveFineCommand(id));
        return Ok(ApiResponse.SuccessResponse("Fine waived."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteFineRecordCommand(id));
        return Ok(ApiResponse.SuccessResponse("Fine deleted."));
    }
}
