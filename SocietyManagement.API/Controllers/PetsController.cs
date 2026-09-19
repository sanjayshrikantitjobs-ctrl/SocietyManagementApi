using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Pets;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/pets")]
public class PetsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Pets.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<PetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] PetType? petType, [FromQuery] int? flatId,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetPetsQuery(societyId, search, petType, flatId, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("summary")]
    [HasPermission(Permissions.Pets.View)]
    [ProducesResponseType(typeof(ApiResponse<PetSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetPetSummaryQuery(societyId));
        return Ok(ApiResponse<PetSummaryDto>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Pets.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreatePetCommand command)
    {
        var id = await Mediator.Send(command);
        return Created(string.Empty, ApiResponse<int>.SuccessResponse(id, "Pet registered."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Pets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdatePetCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Pet updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Pets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeletePetCommand(id));
        return Ok(ApiResponse.SuccessResponse("Pet removed."));
    }
}
