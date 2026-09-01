using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Maintenance;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/maintenance")]
public class MaintenanceBillsController : ApiControllerBase
{
    [HttpGet("bills")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetBills(
        [FromQuery] int societyId, [FromQuery] int? flatId, [FromQuery] BillStatus? status, [FromQuery] DateTime? billMonth,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetBillsQuery(societyId, flatId, status, billMonth, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("bills/{id:int}")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetBillById(int id)
    {
        var result = await Mediator.Send(new GetBillByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("bills/{id:int}/pdf")]
    [HasPermission(Permissions.Maintenance.View)]
    public async Task<IActionResult> GetBillPdf(int id)
    {
        var pdfBytes = await Mediator.Send(new GetBillPdfQuery(id));
        return File(pdfBytes, "application/pdf", $"bill-{id}.pdf");
    }

    [HttpPost("generate")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> Generate([FromBody] GenerateBillsRequest request)
    {
        var count = await Mediator.Send(new GenerateMonthlyBillsCommand(request.SocietyId, request.BillMonth));
        return Ok(ApiResponse<int>.SuccessResponse(count, $"{count} bill(s) generated."));
    }

    [HttpPost("payment")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> RecordPayment(RecordPaymentCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Payment recorded."));
    }

    [HttpPost("bulk-payment")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> BulkRecordPayment(BulkRecordPaymentCommand command)
    {
        var results = await Mediator.Send(command);
        var recordedCount = results.Count(r => r.Recorded);
        return Ok(ApiResponse<object>.SuccessResponse(results, $"{recordedCount} of {results.Count} bill(s) marked paid."));
    }

    [HttpPost("bills/{id:int}/resend-whatsapp")]
    [HasPermission(Permissions.Maintenance.Manage)]
    public async Task<IActionResult> ResendWhatsApp(int id)
    {
        await Mediator.Send(new ResendBillWhatsAppCommand(id));
        return Ok(ApiResponse.SuccessResponse("Bill resent via WhatsApp."));
    }
}

public record GenerateBillsRequest(int SocietyId, DateTime? BillMonth);
