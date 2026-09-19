using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.SocietyDocuments;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/society-documents")]
public class SocietyDocumentsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Documents.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<SocietyDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] SocietyDocumentCategory? category,
        [FromQuery] bool expiringOnly = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetSocietyDocumentsQuery(societyId, search, category, expiringOnly, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Documents.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateSocietyDocumentCommand command)
    {
        var id = await Mediator.Send(command);
        return Created(string.Empty, ApiResponse<int>.SuccessResponse(id, "Document added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Documents.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateSocietyDocumentCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Document updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Documents.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteSocietyDocumentCommand(id));
        return Ok(ApiResponse.SuccessResponse("Document removed."));
    }
}
