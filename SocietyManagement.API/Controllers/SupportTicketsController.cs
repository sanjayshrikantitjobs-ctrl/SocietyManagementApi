using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Support;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/support-tickets")]
public class SupportTicketsController : ApiControllerBase
{
    [HttpGet("mine")]
    [HasPermission(Permissions.SupportTickets.Create)]
    public async Task<IActionResult> GetMine()
    {
        var result = await Mediator.Send(new GetMyTicketsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.SupportTickets.Create)]
    public async Task<IActionResult> Create(CreateSupportTicketCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Ticket submitted."));
    }

    [HttpGet]
    [HasPermission(Permissions.SupportTickets.ManageAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] SupportTicketStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetAllTicketsQuery(status, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPut("{id:int}/status")]
    [HasPermission(Permissions.SupportTickets.ManageAll)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateSupportTicketStatusCommand command)
    {
        if (id != command.Id) return BadRequest();
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Ticket updated."));
    }
}
