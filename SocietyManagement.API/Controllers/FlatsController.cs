using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Flats;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class FlatsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? floorId, [FromQuery] FlatStatus? status, [FromQuery] string? search, [FromQuery] int? societyId,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFlatsQuery(floorId, status, search, societyId, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetFlatByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("mine")]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetMine()
    {
        var result = await Mediator.Send(new GetMyFlatsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Create(CreateFlatCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Flat created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Update(int id, UpdateFlatCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Flat updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteFlatCommand(id));
        return Ok(ApiResponse.SuccessResponse("Flat deleted."));
    }
}
