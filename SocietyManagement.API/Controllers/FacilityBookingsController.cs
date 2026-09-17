using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Facilities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/facility-bookings")]
public class FacilityBookingsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<FacilityBookingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] int? facilityId, [FromQuery] FacilityBookingStatus? status,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFacilityBookingsQuery(societyId, facilityId, status, dateFrom, dateTo, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("mine")]
    [HasPermission(Permissions.Facilities.Book)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<FacilityBookingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] FacilityBookingStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetMyFacilityBookingsQuery(status, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Facilities.Book)]
    [ProducesResponseType(typeof(ApiResponse<FacilityBookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetFacilityBookingByIdQuery(id));
        return Ok(ApiResponse<FacilityBookingDto>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Facilities.Book)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateFacilityBookingCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Facility booked."));
    }

    [HttpPut("{id:int}/status")]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateFacilityBookingStatusRequest request)
    {
        await Mediator.Send(new UpdateFacilityBookingStatusCommand(id, request.Status, request.RejectionReason));
        return Ok(ApiResponse.SuccessResponse("Booking status updated."));
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.Facilities.Book)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelFacilityBookingRequest? request)
    {
        await Mediator.Send(new UpdateFacilityBookingStatusCommand(id, FacilityBookingStatus.Cancelled, request?.Reason));
        return Ok(ApiResponse.SuccessResponse("Booking cancelled."));
    }

    [HttpPut("{id:int}/payment")]
    [HasPermission(Permissions.Facilities.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordFacilityBookingPaymentRequest request)
    {
        await Mediator.Send(new RecordFacilityBookingPaymentCommand(id, request.PaymentStatus));
        return Ok(ApiResponse.SuccessResponse("Payment status updated."));
    }

    public record UpdateFacilityBookingStatusRequest(FacilityBookingStatus Status, string? RejectionReason);
    public record CancelFacilityBookingRequest(string? Reason);
    public record RecordFacilityBookingPaymentRequest(FacilityPaymentStatus PaymentStatus);
}
