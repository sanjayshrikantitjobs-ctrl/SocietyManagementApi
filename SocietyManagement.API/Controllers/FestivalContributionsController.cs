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
        [FromQuery] int festivalId, [FromQuery] string? search, [FromQuery] ContributionPaymentMethod? paymentMethod,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetContributionsQuery(festivalId, search, paymentMethod, pageNumber, pageSize));
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
}
