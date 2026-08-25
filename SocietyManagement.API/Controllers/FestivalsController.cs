using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class FestivalsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] FestivalStatus? status, [FromQuery] int? year,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFestivalsQuery(societyId, status, year, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetFestivalByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Create(CreateFestivalCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Festival created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Update(int id, UpdateFestivalCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Festival updated."));
    }

    [HttpPut("{id:int}/status")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] FestivalStatus status)
    {
        await Mediator.Send(new UpdateFestivalStatusCommand(id, status));
        return Ok(ApiResponse.SuccessResponse("Festival status updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteFestivalCommand(id));
        return Ok(ApiResponse.SuccessResponse("Festival deleted."));
    }

    [HttpGet("contribution-pools")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetContributionPools([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetContributionPoolsQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/pool-summary")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetPoolSummary(int id)
    {
        var result = await Mediator.Send(new GetPoolSummaryQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/pool-status")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetChildPoolStatus(int id)
    {
        var result = await Mediator.Send(new GetChildPoolStatusQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
