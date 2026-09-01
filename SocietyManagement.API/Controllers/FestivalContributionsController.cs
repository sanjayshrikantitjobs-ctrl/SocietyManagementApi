using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/festival-contributions")]
public class FestivalContributionsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int festivalId, [FromQuery] int? flatId, [FromQuery] string? search, [FromQuery] ContributionPaymentMethod? paymentMethod,
        [FromQuery] string? sortBy, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetContributionsQuery(festivalId, flatId, search, paymentMethod, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("top-contributors")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetTopContributors([FromQuery] int festivalId, [FromQuery] int top = 10)
    {
        var result = await Mediator.Send(new GetTopContributorsQuery(festivalId, top));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("pending-contributors")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetPendingContributors([FromQuery] int festivalId)
    {
        var result = await Mediator.Send(new GetPendingContributorsQuery(festivalId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/receipt")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetReceipt(int id)
    {
        var pdfBytes = await Mediator.Send(new GetContributionReceiptPdfQuery(id));
        return File(pdfBytes, "application/pdf", $"receipt-{id}.pdf");
    }

    [HttpPost]
    [HasPermission(Permissions.Festivals.Contribute)]
    public async Task<IActionResult> Create(CreateContributionCommand command)
    {
        var id = await Mediator.Send(command);
        return Created(string.Empty, ApiResponse<int>.SuccessResponse(id, "Contribution recorded."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Festivals.Contribute)]
    public async Task<IActionResult> Update(int id, UpdateContributionCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Contribution updated."));
    }

    [HttpPost("{id:int}/resend-whatsapp")]
    [HasPermission(Permissions.Festivals.Contribute)]
    public async Task<IActionResult> ResendWhatsApp(int id, [FromBody] ResendWhatsAppRequest? request)
    {
        await Mediator.Send(new ResendContributionReceiptCommand(id, request?.WhatsAppNumber));
        return Ok(ApiResponse.SuccessResponse("Receipt resent."));
    }

    [HttpGet("flat-summary")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetFlatSummary(
        [FromQuery] int festivalId, [FromQuery] string? search, [FromQuery] FlatContributionStatus? status,
        [FromQuery] string? sortBy, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFlatContributionsQuery(festivalId, search, status, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("contributable-flats")]
    [HasPermission(Permissions.Festivals.Contribute)]
    public async Task<IActionResult> GetContributableFlats([FromQuery] int festivalId)
    {
        var result = await Mediator.Send(new GetContributableFlatsQuery(festivalId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("flat-summary/kpis")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetFlatSummaryKpis([FromQuery] int festivalId)
    {
        var result = await Mediator.Send(new GetFlatContributionKpisQuery(festivalId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("targets")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> SetTargets(SetContributionTargetsCommand command)
    {
        var count = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(count, $"Target set for {count} flat(s)."));
    }

    [HttpPut("targets")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> UpdateTarget(UpdateFlatContributionTargetCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Target updated."));
    }
}

public record ResendWhatsAppRequest(string? WhatsAppNumber);
