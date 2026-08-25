using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Services;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
public class ServicesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Services.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetSocietyServicesQuery(societyId, search, isActive, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("expiring")]
    [HasPermission(Permissions.Services.View)]
    public async Task<IActionResult> GetExpiring([FromQuery] int societyId, [FromQuery] int withinDays = 10)
    {
        var result = await Mediator.Send(new GetExpiringServicesQuery(societyId, withinDays));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Services.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetSocietyServiceByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Services.Manage)]
    public async Task<IActionResult> Create(CreateSocietyServiceCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Service added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Services.Manage)]
    public async Task<IActionResult> Update(int id, UpdateSocietyServiceCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id and body id must match."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Service updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Services.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteSocietyServiceCommand(id));
        return Ok(ApiResponse.SuccessResponse("Service deleted."));
    }
}
