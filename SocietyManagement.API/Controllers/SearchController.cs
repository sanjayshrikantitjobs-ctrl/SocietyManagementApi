using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.Application.Features.Search;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

// Authenticated only: each source inside the handler is gated on the
// caller's own module permission, so there is no single permission to check here.
[Authorize]
[Route("api/search")]
public class SearchController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SearchResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] int societyId, [FromQuery] string q)
    {
        var result = await Mediator.Send(new GlobalSearchQuery(societyId, q));
        return Ok(ApiResponse<List<SearchResultDto>>.SuccessResponse(result));
    }

    [HttpGet("attention")]
    [ProducesResponseType(typeof(ApiResponse<OperationsAttentionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Attention([FromQuery] int societyId)
    {
        var result = await Mediator.Send(new GetOperationsAttentionQuery(societyId));
        return Ok(ApiResponse<OperationsAttentionDto>.SuccessResponse(result));
    }
}
