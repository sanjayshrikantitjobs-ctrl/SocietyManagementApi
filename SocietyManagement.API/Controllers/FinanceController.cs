using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Finance;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/finance")]
public class FinanceController : ApiControllerBase
{
    [HttpGet("overview")]
    [HasPermission(Permissions.Expenses.View)]
    public async Task<IActionResult> GetOverview([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetFinanceOverviewQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("income")]
    [HasPermission(Permissions.Expenses.View)]
    public async Task<IActionResult> GetIncome(
        [FromQuery] int societyId, [FromQuery] FinanceSource? source, [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFinanceIncomeQuery(societyId, source, dateFrom, dateTo, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("receipts/{source}/{id:int}/pdf")]
    [HasPermission(Permissions.Expenses.View)]
    public async Task<IActionResult> GetReceiptPdf(FinanceSource source, int id)
    {
        var pdfBytes = await Mediator.Send(new GetFinanceReceiptPdfQuery(source, id));
        return File(pdfBytes, "application/pdf", $"receipt-{source}-{id}.pdf");
    }

    [HttpGet("expenses")]
    [HasPermission(Permissions.Expenses.View)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] int societyId, [FromQuery] FinanceSource? source, [FromQuery] ExpenseCategory? category,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFinanceExpensesQuery(societyId, source, category, dateFrom, dateTo, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("expenses/{id:int}")]
    [HasPermission(Permissions.Expenses.View)]
    public async Task<IActionResult> GetExpenseById(int id)
    {
        var result = await Mediator.Send(new GetExpenseByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("expenses")]
    [HasPermission(Permissions.Expenses.Manage)]
    public async Task<IActionResult> CreateExpense(CreateGeneralExpenseCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetExpenseById), new { id }, ApiResponse<int>.SuccessResponse(id, "Expense recorded."));
    }

    [HttpPut("expenses/{id:int}")]
    [HasPermission(Permissions.Expenses.Manage)]
    public async Task<IActionResult> UpdateExpense(int id, UpdateGeneralExpenseCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id and body id must match."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Expense updated."));
    }

    [HttpDelete("expenses/{id:int}")]
    [HasPermission(Permissions.Expenses.Manage)]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        await Mediator.Send(new DeleteGeneralExpenseCommand(id));
        return Ok(ApiResponse.SuccessResponse("Expense deleted."));
    }

    [HttpGet("outstanding")]
    [HasPermission(Permissions.Expenses.View)]
    public async Task<IActionResult> GetOutstanding(
        [FromQuery] int societyId, [FromQuery] FinanceSource? source, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFinanceOutstandingQuery(societyId, source, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("ledger")]
    [HasPermission(Permissions.Expenses.View)]
    public async Task<IActionResult> GetLedger(
        [FromQuery] int societyId, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFinanceLedgerQuery(societyId, dateFrom, dateTo, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("reports/summary")]
    [HasPermission(Permissions.Reports.View)]
    public async Task<IActionResult> GetReportSummary([FromQuery] int societyId, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo)
    {
        var result = await Mediator.Send(new GetFinanceReportSummaryQuery(societyId, dateFrom, dateTo));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("reports/export/pdf")]
    [HasPermission(Permissions.Reports.View)]
    public async Task<IActionResult> ExportReportPdf([FromQuery] int societyId, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo)
    {
        var pdfBytes = await Mediator.Send(new GetFinanceReportPdfQuery(societyId, dateFrom, dateTo));
        return File(pdfBytes, "application/pdf", "financial-report.pdf");
    }

    [HttpGet("reports/export/excel")]
    [HasPermission(Permissions.Reports.View)]
    public async Task<IActionResult> ExportReportExcel([FromQuery] int societyId, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo)
    {
        var excelBytes = await Mediator.Send(new GetFinanceReportExcelQuery(societyId, dateFrom, dateTo));
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "financial-report.xlsx");
    }
}
