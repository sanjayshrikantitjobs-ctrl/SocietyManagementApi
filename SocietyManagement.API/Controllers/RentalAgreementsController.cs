using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Occupancy;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/rental-agreements")]
public class RentalAgreementsController : ApiControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> Create(CreateRentalAgreementCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Rental agreement created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> Update(int id, UpdateRentalAgreementCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id and body id must match."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Rental agreement updated."));
    }
}
