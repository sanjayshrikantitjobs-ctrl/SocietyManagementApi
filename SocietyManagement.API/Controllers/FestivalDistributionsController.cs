using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Festivals;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/festival-distributions")]
public class FestivalDistributionsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse<List<FestivalDistributionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int festivalId)
    {
        var result = await Mediator.Send(new GetDistributionsQuery(festivalId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse<FestivalDistributionDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetDistributionByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateDistributionCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Distribution created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateDistributionCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Distribution updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteDistributionCommand(id));
        return Ok(ApiResponse.SuccessResponse("Distribution deleted."));
    }

    [HttpPost("{id:int}/variants")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddVariant(int id, [FromBody] AddVariantRequest request)
    {
        var variantId = await Mediator.Send(new AddDistributionVariantCommand(id, request.Label));
        return Ok(ApiResponse<int>.SuccessResponse(variantId, "Variant added."));
    }

    [HttpDelete("variants/{variantId:int}")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteVariant(int variantId)
    {
        await Mediator.Send(new DeleteDistributionVariantCommand(variantId));
        return Ok(ApiResponse.SuccessResponse("Variant removed."));
    }

    [HttpPost("{id:int}/generate")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateClaims(int id)
    {
        var count = await Mediator.Send(new GenerateDistributionClaimsCommand(id));
        return Ok(ApiResponse<int>.SuccessResponse(count, $"{count} slot(s) generated."));
    }

    [HttpPost("{id:int}/flats/{flatId:int}")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddManualClaim(int id, int flatId, [FromBody] AddManualClaimRequest request)
    {
        var count = await Mediator.Send(new AddManualClaimCommand(id, flatId, request.Quantity));
        return Ok(ApiResponse<int>.SuccessResponse(count, $"{count} slot(s) added."));
    }

    [HttpDelete("{id:int}/flats/{flatId:int}")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveFlat(int id, int flatId)
    {
        var count = await Mediator.Send(new RemoveFlatClaimsCommand(id, flatId));
        return Ok(ApiResponse<int>.SuccessResponse(count, "Flat removed from this distribution."));
    }

    [HttpPost("{id:int}/flats/{flatId:int}/extra")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddChargeableExtra(int id, int flatId, [FromBody] AddChargeableExtraRequest request)
    {
        var count = await Mediator.Send(new AddChargeableExtraClaimCommand(id, flatId, request.Quantity, request.AmountPerUnit, request.Notes));
        return Ok(ApiResponse<int>.SuccessResponse(count, $"{count} extra item(s) added and charged to the flat."));
    }

    [HttpPost("flats/{flatId:int}/residents")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<FlatMemberOptionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddFlatResident(int flatId, [FromBody] AddFlatResidentRequest request)
    {
        var result = await Mediator.Send(new AddFlatResidentCommand(flatId, request.FirstName, request.LastName, request.Phone, request.Relationship));
        return Ok(ApiResponse<FlatMemberOptionDto>.SuccessResponse(result, "Member added."));
    }

    [HttpGet("{id:int}/claims")]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse<List<DistributionClaimDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClaims(int id, [FromQuery] int? flatId, [FromQuery] DistributionClaimStatus? status)
    {
        var result = await Mediator.Send(new GetDistributionClaimsQuery(id, flatId, status));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}/my-claims")]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse<List<DistributionClaimDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyClaims(int id)
    {
        var result = await Mediator.Send(new GetMyDistributionClaimsQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("flats/{flatId:int}/members")]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse<List<FlatMemberOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlatMembers(int flatId)
    {
        var result = await Mediator.Send(new GetFlatMembersForDistributionQuery(flatId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    // Claim-lifecycle actions are gated at Festivals.View — the handler
    // itself enforces the real authorization (ManageDistribution holders
    // may act on any flat's claim; everyone else only their own flat's,
    // resolved from their own Member/FlatResidency rows), the same
    // ownership-in-handler pattern GetMyRsvpQuery already uses elsewhere.

    [HttpPost("claims/{claimId:int}/assign-member")]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignMember(int claimId, [FromBody] AssignClaimMemberRequest request)
    {
        await Mediator.Send(new AssignClaimMemberCommand(claimId, request.MemberId));
        return Ok(ApiResponse.SuccessResponse("Assignment updated."));
    }

    [HttpPost("claims/{claimId:int}/select")]
    [HasPermission(Permissions.Festivals.View)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectVariant(int claimId, [FromBody] SelectClaimVariantRequest request)
    {
        await Mediator.Send(new SelectClaimVariantCommand(claimId, request.VariantId, request.MemberId));
        return Ok(ApiResponse.SuccessResponse("Selection saved."));
    }

    [HttpPost("claims/{claimId:int}/distribute")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkDistributed(int claimId)
    {
        await Mediator.Send(new MarkClaimDistributedCommand(claimId));
        return Ok(ApiResponse.SuccessResponse("Marked as distributed."));
    }

    [HttpPost("{id:int}/flats/{flatId:int}/distribute")]
    [HasPermission(Permissions.Festivals.ManageDistribution)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkFlatDistributed(int id, int flatId)
    {
        var count = await Mediator.Send(new MarkFlatDistributedCommand(id, flatId));
        return Ok(ApiResponse<int>.SuccessResponse(count, $"{count} item(s) marked as distributed."));
    }
}

public record AddVariantRequest(string Label);
public record AssignClaimMemberRequest(int? MemberId);
public record SelectClaimVariantRequest(int? VariantId, int? MemberId);
public record AddManualClaimRequest(int Quantity);
public record AddChargeableExtraRequest(int Quantity, decimal AmountPerUnit, string? Notes);
public record AddFlatResidentRequest(string FirstName, string LastName, string? Phone, PersonRelationship Relationship);
