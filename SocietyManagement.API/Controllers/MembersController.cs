using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Residents;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/members")]
public class MembersController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int societyId, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetMembersQuery(societyId, search, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Members.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetMemberByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Members.Create)]
    public async Task<IActionResult> Create(CreateMemberCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Member added."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> Update(int id, UpdateMemberCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Member updated."));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Members.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteMemberCommand(id));
        return Ok(ApiResponse.SuccessResponse("Member deleted."));
    }

    [HttpPost("{id:int}/create-login")]
    [HasPermission(Permissions.Members.Update)]
    public async Task<IActionResult> CreateLogin(int id, [FromBody] CreateLoginRequest request)
    {
        var userId = await Mediator.Send(new CreateUserForMemberCommand(id, request.RoleId, request.Password));
        return Ok(ApiResponse<int>.SuccessResponse(userId, "Login account created."));
    }

    [HttpGet("import/template")]
    [HasPermission(Permissions.Members.Create)]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        var bytes = await Mediator.Send(new GetResidentImportTemplateQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "resident-import-template.xlsx");
    }

    [HttpPost("import")]
    [HasPermission(Permissions.Members.Create)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] int societyId)
    {
        if (file.Length == 0) throw new BadRequestAppException("No file was uploaded.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        var result = await Mediator.Send(new ImportResidentsCommand(societyId, stream.ToArray()));
        return Ok(ApiResponse<ResidentImportResultDto>.SuccessResponse(result,
            $"{result.FlatsCreated} flat(s), {result.MembersCreated} owner(s) and {result.ResidenciesCreated} residency link(s) added."));
    }
}

public record CreateLoginRequest(int RoleId, string? Password = null);
