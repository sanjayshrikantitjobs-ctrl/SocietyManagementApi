using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Complaints;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/complaints")]
public class ComplaintsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Complaints.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] ComplaintCategory? category, [FromQuery] ComplaintPriority? priority,
        [FromQuery] string? search)
    {
        var result = await Mediator.Send(new GetComplaintsQuery(societyId, category, priority, search));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("paged")]
    [HasPermission(Permissions.Complaints.View)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int societyId, [FromQuery] ComplaintCategory? category, [FromQuery] ComplaintPriority? priority,
        [FromQuery] string? search, [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetComplaintsPagedQuery(societyId, category, priority, search, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("kpis")]
    [HasPermission(Permissions.Complaints.View)]
    public async Task<IActionResult> GetKpis([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetComplaintKpisQuery(societyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("mine")]
    [HasPermission(Permissions.Complaints.Create)]
    public async Task<IActionResult> GetMine()
    {
        var result = await Mediator.Send(new GetMyComplaintsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Complaints.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetComplaintByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Complaints.Create)]
    public async Task<IActionResult> Create(CreateComplaintCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Complaint raised."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Complaints.Create)]
    public async Task<IActionResult> Update(int id, UpdateComplaintCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id and body id must match."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Complaint updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Complaints.Create)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteComplaintCommand(id));
        return Ok(ApiResponse.SuccessResponse("Complaint deleted."));
    }

    [HttpPost("{id:int}/assign")]
    [HasPermission(Permissions.Complaints.Manage)]
    public async Task<IActionResult> Assign(int id, AssignComplaintRequest request)
    {
        await Mediator.Send(new AssignComplaintCommand(id, request.StaffId));
        return Ok(ApiResponse.SuccessResponse("Complaint assigned."));
    }

    [HttpPost("{id:int}/start")]
    [HasPermission(Permissions.Complaints.Manage)]
    public async Task<IActionResult> Start(int id)
    {
        await Mediator.Send(new StartProgressCommand(id));
        return Ok(ApiResponse.SuccessResponse("Complaint moved to in progress."));
    }

    [HttpPost("{id:int}/resolve")]
    [HasPermission(Permissions.Complaints.Manage)]
    public async Task<IActionResult> Resolve(int id, ResolveComplaintRequest request)
    {
        await Mediator.Send(new ResolveComplaintCommand(id, request.ResolutionNotes));
        return Ok(ApiResponse.SuccessResponse("Complaint resolved."));
    }

    [HttpPost("{id:int}/close")]
    [HasPermission(Permissions.Complaints.Create)]
    public async Task<IActionResult> Close(int id)
    {
        await Mediator.Send(new CloseComplaintCommand(id));
        return Ok(ApiResponse.SuccessResponse("Complaint closed."));
    }

    [HttpPost("{id:int}/reopen")]
    [HasPermission(Permissions.Complaints.Create)]
    public async Task<IActionResult> Reopen(int id, ReopenComplaintRequest request)
    {
        await Mediator.Send(new ReopenComplaintCommand(id, request.Reason));
        return Ok(ApiResponse.SuccessResponse("Complaint reopened."));
    }
}

public record AssignComplaintRequest(int StaffId);
public record ResolveComplaintRequest(string ResolutionNotes);
public record ReopenComplaintRequest(string Reason);
