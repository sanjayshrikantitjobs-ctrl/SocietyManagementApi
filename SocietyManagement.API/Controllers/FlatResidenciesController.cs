using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Residents;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/flat-residencies")]
public class FlatResidenciesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetAll([FromQuery] int flatId)
    {
        var result = await Mediator.Send(new GetResidenciesQuery(flatId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("occupancy-summary")]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetOccupancySummary([FromQuery] int flatId)
    {
        var result = await Mediator.Send(new GetFlatOccupancySummaryQuery(flatId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("ownership-history")]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetOwnershipHistory([FromQuery] int flatId)
    {
        var result = await Mediator.Send(new GetOwnershipHistoryQuery(flatId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Members.Create)]
    public async Task<IActionResult> Create(CreateResidencyCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { flatId = command.FlatId }, ApiResponse<int>.SuccessResponse(id, "Residency recorded."));
    }

    [HttpPost("{id:int}/end")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> End(int id, [FromBody] EndResidencyRequest request)
    {
        await Mediator.Send(new EndResidencyCommand(id, request.MoveOutDate));
        return Ok(ApiResponse.SuccessResponse("Residency ended."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Members.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteResidencyCommand(id));
        return Ok(ApiResponse.SuccessResponse("Residency deleted."));
    }
}

public record EndResidencyRequest(DateTime MoveOutDate);
