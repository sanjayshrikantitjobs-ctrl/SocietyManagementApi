using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Occupancy;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Documents (Possession Letter, Parking Allotment Letter, Tenant
/// Police NOC, Rental Agreement, Other) attached to one FlatOccupancy
/// episode — see ResidentDocument's own doc comment.</summary>
[Authorize]
[Route("api/resident-documents")]
public class ResidentDocumentsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetAll([FromQuery] int flatOccupancyId)
    {
        var result = await Mediator.Send(new GetResidentDocumentsQuery(flatOccupancyId));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> Upload(UploadResidentDocumentCommand command)
    {
        var id = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Document uploaded."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteResidentDocumentCommand(id));
        return Ok(ApiResponse.SuccessResponse("Document removed."));
    }
}
