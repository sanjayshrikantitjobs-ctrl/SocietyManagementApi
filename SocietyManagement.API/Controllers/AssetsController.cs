using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Assets;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/assets")]
public class AssetsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Assets.View)]
    [ProducesResponseType(typeof(ApiResponse<List<AssetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int societyId, [FromQuery] bool activeOnly = false)
    {
        var result = await Mediator.Send(new GetAssetsQuery(societyId, activeOnly));
        return Ok(ApiResponse<List<AssetDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Assets.View)]
    [ProducesResponseType(typeof(ApiResponse<AssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetAssetByIdQuery(id));
        return Ok(ApiResponse<AssetDto>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/availability")]
    [HasPermission(Permissions.Assets.View)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailability(int id, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var result = await Mediator.Send(new GetAssetAvailabilityQuery(id, startDate, endDate));
        return Ok(ApiResponse<int>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateAssetCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Asset created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateAssetCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Asset updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteAssetCommand(id));
        return Ok(ApiResponse.SuccessResponse("Asset deleted."));
    }
}
