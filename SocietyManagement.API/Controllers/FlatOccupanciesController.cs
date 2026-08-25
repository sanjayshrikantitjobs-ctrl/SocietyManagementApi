using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Occupancy;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/flat-occupancies")]
public class FlatOccupanciesController : ApiControllerBase
{
    [HttpGet("overview")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetOverview([FromQuery] int flatId)
    {
        var result = await Mediator.Send(new GetFlatOccupancyOverviewQuery(flatId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("owners-grid")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetOwnersGrid(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFlatsOwnershipGridQuery(societyId, search, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("tenants-grid")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetTenantsGrid(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetFlatsTenancyGridQuery(societyId, search, sortBy, sortDescending, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/members")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetMembers(int id)
    {
        var result = await Mediator.Send(new GetOccupancyMembersQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("mine/members")]
    [HasPermission(Permissions.Occupancy.ManageOwn)]
    public async Task<IActionResult> GetMyMembers()
    {
        var result = await Mediator.Send(new GetMyFamilyMembersQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("history")]
    [HasPermission(Permissions.Occupancy.ViewHistory)]
    public async Task<IActionResult> GetHistory([FromQuery] int flatId, [FromQuery] OccupancyType? type)
    {
        var result = await Mediator.Send(new GetOccupancyHistoryQuery(flatId, type));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("owner-member")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> AddOwnerMember(AddOwnerMemberCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Owner added."));
    }

    [HttpPost("tenant")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> AddTenant(AddTenantOccupancyCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Tenant added."));
    }

    [HttpPost("{id:int}/family-member")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> AddFamilyMember(int id, [FromBody] AddFamilyMemberRequest request)
    {
        var command = new AddTenantFamilyMemberCommand(
            id, request.PersonId, request.FirstName, request.LastName, request.Phone, request.Email, request.WhatsAppNumber,
            request.Gender, request.DateOfBirth, request.PhotoUrl, request.AadhaarNumber, request.PanNumber,
            request.Relationship, request.MoveInDate);
        var memberId = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(memberId, "Family member added."));
    }

    [HttpPost("mine/members")]
    [HasPermission(Permissions.Occupancy.ManageOwn)]
    public async Task<IActionResult> AddMyFamilyMember(AddMyFamilyMemberCommand command)
    {
        var memberId = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(memberId, "Family member added."));
    }

    [HttpPost("{id:int}/end")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> End(int id, [FromBody] EndOccupancyRequest request)
    {
        await Mediator.Send(new EndOccupancyCommand(id, request.EndDate));
        return Ok(ApiResponse.SuccessResponse("Occupancy ended."));
    }

    [HttpPost("members/{memberId:int}/remove")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> RemoveMember(int memberId, [FromBody] RemoveOccupancyMemberRequest request)
    {
        await Mediator.Send(new RemoveOccupancyMemberCommand(memberId, request.LeftDate));
        return Ok(ApiResponse.SuccessResponse("Member removed."));
    }

    [HttpPut("members/{memberId:int}")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> UpdateMember(int memberId, [FromBody] UpdateOccupancyMemberRequest request)
    {
        await Mediator.Send(new UpdateOccupancyMemberCommand(memberId, request.Relationship, request.ResidentStatus));
        return Ok(ApiResponse.SuccessResponse("Member updated."));
    }
}

public record AddFamilyMemberRequest(
    int? PersonId, string? FirstName, string? LastName, string? Phone, string? Email, string? WhatsAppNumber,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl, string? AadhaarNumber, string? PanNumber,
    PersonRelationship Relationship, DateTime MoveInDate);

public record EndOccupancyRequest(DateTime EndDate);

public record RemoveOccupancyMemberRequest(DateTime LeftDate);

public record UpdateOccupancyMemberRequest(PersonRelationship Relationship, ResidentStatus ResidentStatus);
