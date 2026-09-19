using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Budgets;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

// Budget vs actual is finance-admin only (expenses.manage): residents hold
// expenses.view, but budgets are a planning tool, not shared expense history.
[Authorize]
[Route("api/budgets")]
public class BudgetsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Expenses.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BudgetOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview([FromQuery] int societyId, [FromQuery] int financialYear)
    {
        var result = await Mediator.Send(new GetBudgetOverviewQuery(societyId, financialYear));
        return Ok(ApiResponse<BudgetOverviewDto>.SuccessResponse(result));
    }

    [HttpPut]
    [HasPermission(Permissions.Expenses.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Set(SetBudgetCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Budget saved."));
    }
}
