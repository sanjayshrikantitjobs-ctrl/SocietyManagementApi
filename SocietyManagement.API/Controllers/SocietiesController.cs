using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Societies.Commands;
using SocietyManagement.Application.Features.Societies.Common;
using SocietyManagement.Application.Features.Societies.Queries;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Module 1: Society Setup — root entity of the Society -> Building ->
/// Wing -> Floor -> Flat -> Parking hierarchy.</summary>
[Authorize]
public class SocietiesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Society.View)]
    [ProducesResponseType(typeof(ApiResponse<List<SocietyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await Mediator.Send(new GetSocietiesQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Society.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetSocietyByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Society.Create)]
    public async Task<IActionResult> Create(CreateSocietyCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Society created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Update(int id, UpdateSocietyCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Society updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Society.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteSocietyCommand(id));
        return Ok(ApiResponse.SuccessResponse("Society deleted."));
    }

    /// <summary>Super Admin only — gated by Permissions.Society.Create (the
    /// one permission never granted to a regular Admin) plus the handler's
    /// own inline check, same belt-and-suspenders pattern used elsewhere.</summary>
    [HttpPut("{id:int}/subscription")]
    [HasPermission(Permissions.Society.Create)]
    public async Task<IActionResult> SetSubscription(int id, SetSocietySubscriptionCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Subscription updated."));
    }

    /// <summary>Manual restrict/reinstate toggle, independent of the date
    /// window above — same Super Admin-only gating.</summary>
    [HttpPut("{id:int}/suspension")]
    [HasPermission(Permissions.Society.Create)]
    public async Task<IActionResult> SetSuspension(int id, SetSocietySuspensionCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse(command.IsSuspended ? "Society restricted." : "Society reinstated."));
    }
}
