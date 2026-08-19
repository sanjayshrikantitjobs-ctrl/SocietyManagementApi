using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/festival-budget-categories")]
public class FestivalBudgetCategoriesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetAll([FromQuery] int festivalId)
    {
        var result = await Mediator.Send(new GetBudgetCategoriesQuery(festivalId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/revisions")]
    [HasPermission(Permissions.Festivals.View)]
    public async Task<IActionResult> GetRevisions(int id)
    {
        var result = await Mediator.Send(new GetBudgetRevisionsQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Create(CreateBudgetCategoryCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { festivalId = command.FestivalId }, ApiResponse<int>.SuccessResponse(id, "Budget category created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Update(int id, UpdateBudgetCategoryCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Budget category updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Festivals.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteBudgetCategoryCommand(id));
        return Ok(ApiResponse.SuccessResponse("Budget category deleted."));
    }
}
