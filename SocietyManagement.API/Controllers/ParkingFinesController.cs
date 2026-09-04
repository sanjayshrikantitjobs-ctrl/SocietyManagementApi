using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.ParkingFines;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Watchman-created parking-violation fines, evidenced by an
/// optional photo — see ParkingFine's own doc comment. Watchman holds
/// View/Create only; Delete is Admin/SuperAdmin-only.</summary>
[Authorize]
public class ParkingFinesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ParkingFines.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<ParkingFineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] int? vehicleId, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetParkingFinesQuery(societyId, vehicleId, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.ParkingFines.Create)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreateParkingFineCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Parking fine recorded."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ParkingFines.Delete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteParkingFineCommand(id));
        return Ok(ApiResponse.SuccessResponse("Parking fine removed."));
    }
}
