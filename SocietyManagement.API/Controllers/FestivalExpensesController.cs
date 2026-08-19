using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/festival-expenses")]
public class FestivalExpensesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int festivalId, [FromQuery] ExpenseApprovalStatus? status, [FromQuery] int? festivalBudgetCategoryId,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetExpensesQuery(festivalId, status, festivalBudgetCategoryId, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Create(CreateExpenseCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { festivalId = command.FestivalId }, ApiResponse<int>.SuccessResponse(id, "Expense recorded."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Update(int id, UpdateExpenseCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Expense updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteExpenseCommand(id));
        return Ok(ApiResponse.SuccessResponse("Expense deleted."));
    }

    [HttpPost("{id:int}/submit")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Submit(int id)
    {
        await Mediator.Send(new SubmitExpenseCommand(id));
        return Ok(ApiResponse.SuccessResponse("Expense submitted for approval."));
    }

    [HttpPost("{id:int}/approve")]
    [HasPermission(Permissions.Festivals.ApproveExpense)]
    public async Task<IActionResult> Approve(int id)
    {
        await Mediator.Send(new ApproveExpenseCommand(id));
        return Ok(ApiResponse.SuccessResponse("Expense approved."));
    }

    [HttpPost("{id:int}/reject")]
    [HasPermission(Permissions.Festivals.ApproveExpense)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectExpenseRequest request)
    {
        await Mediator.Send(new RejectExpenseCommand(id, request.Reason));
        return Ok(ApiResponse.SuccessResponse("Expense rejected."));
    }

    [HttpPost("{id:int}/mark-paid")]
    [HasPermission(Permissions.Festivals.ApproveExpense)]
    public async Task<IActionResult> MarkPaid(int id)
    {
        await Mediator.Send(new MarkExpensePaidCommand(id));
        return Ok(ApiResponse.SuccessResponse("Expense marked as paid."));
    }
}

public record RejectExpenseRequest(string Reason);
