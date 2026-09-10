using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Residents;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/emergency-contacts")]
public class EmergencyContactsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Members.View)]
    [ProducesResponseType(typeof(ApiResponse<List<EmergencyContactDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int flatId)
    {
        var result = await Mediator.Send(new GetEmergencyContactsQuery(flatId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Members.Create)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateEmergencyContactCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { flatId = command.FlatId }, ApiResponse<int>.SuccessResponse(id, "Emergency contact added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Members.Update)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateEmergencyContactCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Emergency contact updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Members.Delete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteEmergencyContactCommand(id));
        return Ok(ApiResponse.SuccessResponse("Emergency contact deleted."));
    }
}
