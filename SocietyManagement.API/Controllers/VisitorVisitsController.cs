using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Visitors;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/visitor-visits")]
public class VisitorVisitsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Visitors.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<VisitorVisitDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] VisitorVisitStatus? status, [FromQuery] int? gateId,
        [FromQuery] int? flatId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = true,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetVisitsQuery(
            societyId, status, gateId, flatId, fromDate, toDate, search, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("pending")]
    [HasPermission(Permissions.Visitors.View)]
    [ProducesResponseType(typeof(ApiResponse<List<VisitorVisitDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending()
    {
        var result = await Mediator.Send(new GetPendingApprovalsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("mine")]
    [HasPermission(Permissions.Visitors.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<VisitorVisitDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = true,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetMyVisitsQuery(fromDate, toDate, search, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("currently-inside")]
    [HasPermission(Permissions.Visitors.View)]
    [ProducesResponseType(typeof(ApiResponse<List<VisitorVisitDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentlyInside([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetCurrentlyInsideQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Visitors.Create)]
    [ProducesResponseType(typeof(ApiResponse<VisitorVisitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreateVisitCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Visitor request created."));
    }

    [HttpPost("{id:int}/approve")]
    [HasPermission(Permissions.Visitors.Approve)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(int id)
    {
        await Mediator.Send(new ApproveVisitCommand(id));
        return Ok(ApiResponse.SuccessResponse("Visitor approved."));
    }

    [HttpPost("{id:int}/reject")]
    [HasPermission(Permissions.Visitors.Reject)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectVisitRequest request)
    {
        await Mediator.Send(new RejectVisitCommand(id, request.Reason));
        return Ok(ApiResponse.SuccessResponse("Visitor rejected."));
    }

    [HttpPost("{id:int}/check-in")]
    [HasPermission(Permissions.Visitors.CheckIn)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIn(int id)
    {
        await Mediator.Send(new CheckInVisitCommand(id));
        return Ok(ApiResponse.SuccessResponse("Visitor checked in."));
    }

    [HttpPost("{id:int}/check-out")]
    [HasPermission(Permissions.Visitors.CheckOut)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckOut(int id)
    {
        await Mediator.Send(new CheckOutVisitCommand(id));
        return Ok(ApiResponse.SuccessResponse("Visitor checked out."));
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.Visitors.Create)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(int id)
    {
        await Mediator.Send(new CancelVisitCommand(id));
        return Ok(ApiResponse.SuccessResponse("Visitor request cancelled."));
    }
}

public record RejectVisitRequest(string? Reason);
