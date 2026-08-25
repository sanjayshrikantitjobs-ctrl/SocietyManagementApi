using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Committee;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Society-level committee directory (Chairman/Secretary/Treasurer/
/// etc.) — View is granted to residents (Member role + Admin/SuperAdmin),
/// Manage is Admin/SuperAdmin only.</summary>
[Authorize]
public class CommitteeController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Committee.View)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetCommitteeMembersQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Committee.Manage)]
    public async Task<IActionResult> Create(CreateCommitteeMemberCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), ApiResponse<int>.SuccessResponse(id, "Committee member added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Committee.Manage)]
    public async Task<IActionResult> Update(int id, UpdateCommitteeMemberCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Committee member updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Committee.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteCommitteeMemberCommand(id));
        return Ok(ApiResponse.SuccessResponse("Committee member deleted."));
    }
}
