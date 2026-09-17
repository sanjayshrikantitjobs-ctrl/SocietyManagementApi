using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Assets;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/asset-bookings")]
public class AssetBookingsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AssetBookingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] AssetBookingStatus? status,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetAssetBookingsQuery(societyId, status, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("mine")]
    [HasPermission(Permissions.Assets.Rent)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AssetBookingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] AssetBookingStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetMyAssetBookingsQuery(status, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Assets.Rent)]
    [ProducesResponseType(typeof(ApiResponse<AssetBookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetAssetBookingByIdQuery(id));
        return Ok(ApiResponse<AssetBookingDto>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Assets.Rent)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateAssetBookingCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Rental request submitted."));
    }

    [HttpPut("{id:int}/status")]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAssetBookingStatusRequest request)
    {
        await Mediator.Send(new UpdateAssetBookingStatusCommand(id, request.Status, request.RejectionReason));
        return Ok(ApiResponse.SuccessResponse("Rental status updated."));
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.Assets.Rent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelAssetBookingRequest? request)
    {
        await Mediator.Send(new UpdateAssetBookingStatusCommand(id, AssetBookingStatus.Cancelled, request?.Reason));
        return Ok(ApiResponse.SuccessResponse("Rental request cancelled."));
    }

    [HttpPut("items/{itemId:int}/issue")]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordIssue(int itemId, [FromBody] RecordIssueRequest request)
    {
        await Mediator.Send(new RecordAssetIssueCommand(itemId, request.QuantityIssued));
        return Ok(ApiResponse.SuccessResponse("Issued quantity recorded."));
    }

    [HttpPut("items/{itemId:int}/return")]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordReturn(int itemId, [FromBody] RecordReturnRequest request)
    {
        await Mediator.Send(new RecordAssetReturnCommand(itemId, request.QuantityReturned, request.QuantityDamaged, request.QuantityLost));
        return Ok(ApiResponse.SuccessResponse("Return recorded."));
    }

    [HttpPut("{id:int}/deposit-refund")]
    [HasPermission(Permissions.Assets.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordDepositRefund(int id, [FromBody] RecordDepositRefundRequest request)
    {
        await Mediator.Send(new RecordAssetDepositRefundCommand(id, request.DepositRefundAmount));
        return Ok(ApiResponse.SuccessResponse("Deposit refund recorded."));
    }

    public record UpdateAssetBookingStatusRequest(AssetBookingStatus Status, string? RejectionReason);
    public record CancelAssetBookingRequest(string? Reason);
    public record RecordIssueRequest(int QuantityIssued);
    public record RecordReturnRequest(int QuantityReturned, int QuantityDamaged, int QuantityLost);
    public record RecordDepositRefundRequest(decimal DepositRefundAmount);
}
