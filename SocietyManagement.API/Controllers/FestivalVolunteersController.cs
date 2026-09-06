using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/festival-volunteers")]
public class FestivalVolunteersController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse<List<FestivalVolunteerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int festivalId)
    {
        var result = await Mediator.Send(new GetVolunteersQuery(festivalId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Festivals.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateVolunteerCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { festivalId = command.FestivalId }, ApiResponse<int>.SuccessResponse(id, "Volunteer added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateVolunteerCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Volunteer updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteVolunteerCommand(id));
        return Ok(ApiResponse.SuccessResponse("Volunteer deleted."));
    }
}
