using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Vehicles;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Vehicle Security console — manual plate entry (optionally with a
/// photo attached for the record), manual search, and scan history.
/// Deliberately separate from VehiclesController (the existing Vehicle CRUD,
/// still gated on Members.*) so Watchman can reach this without the broader
/// Members grant. Never creates a Vehicle record — see
/// VehicleScanFeature.cs.</summary>
[Authorize]
[Route("api/vehicle-scans")]
public class VehicleScansController : ApiControllerBase
{
    [HttpPost("confirm")]
    [HasPermission(Permissions.Vehicles.Scan)]
    [ProducesResponseType(typeof(ApiResponse<VehicleScanResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(ConfirmVehicleScanRequest request)
    {
        var result = await Mediator.Send(new ConfirmVehicleScanCommand(
            request.SocietyId, request.NormalizedRegistrationNumber, request.RawOcrText, request.Confidence,
            request.Source, request.GateId, request.ImageBytes));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("search")]
    [HasPermission(Permissions.Vehicles.Search)]
    [ProducesResponseType(typeof(ApiResponse<List<VehicleSearchItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] int societyId, [FromQuery] string query)
    {
        var result = await Mediator.Send(new GetVehicleSearchQuery(societyId, query));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("history")]
    [HasPermission(Permissions.Vehicles.Scan)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<VehicleScanHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(
        [FromQuery] int societyId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] VehicleScanResultStatus? result, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var history = await Mediator.Send(new GetScanHistoryQuery(societyId, fromDate, toDate, result, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(history));
    }
}

/// <summary>JSON body for POST /confirm — ImageBytes binds from a base64
/// string automatically (System.Text.Json's native byte[] handling), only
/// present when the user attached a photo to the manual entry.</summary>
public record ConfirmVehicleScanRequest(
    int SocietyId, string NormalizedRegistrationNumber, string? RawOcrText, double? Confidence,
    VehicleScanSource Source, int? GateId, byte[]? ImageBytes);
