using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Vendors;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/vendors")]
public class VendorsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Vendors.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<VendorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] ServiceVendorCategory? category, [FromQuery] bool? isActive,
        [FromQuery] int? expiringWithinDays, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetVendorsQuery(societyId, search, category, isActive, expiringWithinDays, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Vendors.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateVendorCommand command)
    {
        var id = await Mediator.Send(command);
        return Created(string.Empty, ApiResponse<int>.SuccessResponse(id, "Vendor added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Vendors.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateVendorCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Vendor updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Vendors.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteVendorCommand(id));
        return Ok(ApiResponse.SuccessResponse("Vendor removed."));
    }
}
