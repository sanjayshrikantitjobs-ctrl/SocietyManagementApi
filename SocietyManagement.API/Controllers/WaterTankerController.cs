using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/water-tanker")]
public class WaterTankerController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] DateTime month, [FromQuery] bool? isPaid, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetWaterTankerCollectionsQuery(societyId, month, isPaid, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("months")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetMonths([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetWaterTankerMonthsQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("summary")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetSummary([FromQuery] int societyId, [FromQuery] DateTime month)
    {
        var result = await Mediator.Send(new GetWaterTankerSummaryQuery(societyId, month));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("mine")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetMine()
    {
        var result = await Mediator.Send(new GetMyWaterTankerCollectionsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("generate")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Generate(GenerateWaterTankerChargesCommand command)
    {
        var count = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(count, $"Charge created for {count} flat(s)."));
    }

    [HttpPost("{id:int}/pay")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordPaymentRequest request)
    {
        await Mediator.Send(new RecordWaterTankerPaymentCommand(id, request.PaymentDate, request.Notes));
        return Ok(ApiResponse.SuccessResponse("Payment recorded."));
    }
}

public record RecordPaymentRequest(DateTime PaymentDate, string? Notes);
