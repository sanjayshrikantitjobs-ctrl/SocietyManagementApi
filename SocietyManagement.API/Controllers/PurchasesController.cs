using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Purchasing;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/purchases")]
public class PurchasesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Purchases.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<PurchaseRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] PurchaseStatus? status,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetPurchaseRequestsQuery(societyId, search, status, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Purchases.View)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetPurchaseRequestByIdQuery(id));
        return Ok(ApiResponse<PurchaseRequestDto>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Purchases.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreatePurchaseRequestCommand command)
    {
        var id = await Mediator.Send(command);
        return Created(string.Empty, ApiResponse<int>.SuccessResponse(id, "Purchase request created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Purchases.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdatePurchaseRequestCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Purchase request updated."));
    }

    [HttpPost("{id:int}/submit")]
    [HasPermission(Permissions.Purchases.Manage)]
    public async Task<IActionResult> Submit(int id)
    {
        await Mediator.Send(new SubmitPurchaseRequestCommand(id));
        return Ok(ApiResponse.SuccessResponse("Submitted for approval."));
    }

    [HttpPost("{id:int}/approve")]
    [HasPermission(Permissions.Purchases.Approve)]
    public async Task<IActionResult> Approve(int id)
    {
        await Mediator.Send(new ApprovePurchaseRequestCommand(id));
        return Ok(ApiResponse.SuccessResponse("Approved."));
    }

    [HttpPost("{id:int}/reject")]
    [HasPermission(Permissions.Purchases.Approve)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectPurchaseBody body)
    {
        await Mediator.Send(new RejectPurchaseRequestCommand(id, body.Reason));
        return Ok(ApiResponse.SuccessResponse("Rejected."));
    }

    [HttpPost("{id:int}/order")]
    [HasPermission(Permissions.Purchases.Manage)]
    public async Task<IActionResult> Order(int id, [FromBody] OrderPurchaseBody body)
    {
        await Mediator.Send(new MarkPurchaseOrderedCommand(id, body.VendorId));
        return Ok(ApiResponse.SuccessResponse("Order placed."));
    }

    [HttpPost("{id:int}/receive")]
    [HasPermission(Permissions.Purchases.Manage)]
    public async Task<IActionResult> Receive(int id, [FromBody] ReceivePurchaseBody body)
    {
        await Mediator.Send(new ReceivePurchaseCommand(id, body.Lines));
        return Ok(ApiResponse.SuccessResponse("Goods received."));
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.Purchases.Manage)]
    public async Task<IActionResult> Cancel(int id)
    {
        await Mediator.Send(new CancelPurchaseRequestCommand(id));
        return Ok(ApiResponse.SuccessResponse("Cancelled."));
    }
}

public record RejectPurchaseBody(string Reason);
public record OrderPurchaseBody(int? VendorId);
public record ReceivePurchaseBody(List<ReceiveLine> Lines);
