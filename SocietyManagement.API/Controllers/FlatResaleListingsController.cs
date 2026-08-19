using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Residents;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/flat-resale-listings")]
public class FlatResaleListingsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] ListingStatus? status,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetListingsQuery(societyId, status, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetListingByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Members.Create)]
    public async Task<IActionResult> Create(CreateListingCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Listing created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> Update(int id, UpdateListingCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Listing updated."));
    }

    [HttpPost("{id:int}/under-negotiation")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> MarkUnderNegotiation(int id)
    {
        await Mediator.Send(new MarkUnderNegotiationCommand(id));
        return Ok(ApiResponse.SuccessResponse("Listing marked under negotiation."));
    }

    [HttpPost("{id:int}/sold")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> MarkSold(int id)
    {
        await Mediator.Send(new MarkSoldCommand(id));
        return Ok(ApiResponse.SuccessResponse("Listing marked sold."));
    }

    [HttpPost("{id:int}/withdraw")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> Withdraw(int id)
    {
        await Mediator.Send(new WithdrawListingCommand(id));
        return Ok(ApiResponse.SuccessResponse("Listing withdrawn."));
    }

    [HttpPost("{id:int}/issue-noc")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> IssueNoc(int id, IssueNocRequest request)
    {
        await Mediator.Send(new IssueNocCommand(id, request.NocIssuedDate, request.NocDocumentUrl));
        return Ok(ApiResponse.SuccessResponse("NOC issued."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Members.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteListingCommand(id));
        return Ok(ApiResponse.SuccessResponse("Listing deleted."));
    }
}

public record IssueNocRequest(DateTime NocIssuedDate, string NocDocumentUrl);
