using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.Application.Features.Files;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Generic multipart upload backing every banner/logo/bill-image/NOC
/// picker across modules (Festivals, Maintenance, Residents, ...). Gated on
/// authentication only — uploading bytes isn't itself sensitive; the specific
/// command that later links the returned URL to a record (e.g. IssueNocCommand)
/// carries its own module permission check.</summary>
[Authorize]
public class FilesController : ApiControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string folder)
    {
        if (file.Length == 0) throw new BadRequestAppException("No file was uploaded.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        var url = await Mediator.Send(new UploadFileCommand(stream.ToArray(), file.FileName, folder));
        return Ok(ApiResponse<string>.SuccessResponse(url, "File uploaded."));
    }
}
