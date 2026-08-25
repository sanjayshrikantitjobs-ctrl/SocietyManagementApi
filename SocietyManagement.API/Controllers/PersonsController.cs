using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Occupancy;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/persons")]
public class PersonsController : ApiControllerBase
{
    [HttpGet("search")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> Search([FromQuery] int societyId, [FromQuery] string phone)
    {
        var result = await Mediator.Send(new SearchPersonsQuery(societyId, phone));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetPersonByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> Create(CreatePersonCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<int>.SuccessResponse(id, "Person created."));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> Update(int id, UpdatePersonCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id and body id must match."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Person updated."));
    }

    [HttpGet("{id:int}/login")]
    [HasPermission(Permissions.Occupancy.View)]
    public async Task<IActionResult> GetLogin(int id)
    {
        var result = await Mediator.Send(new GetPersonLoginQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("{id:int}/create-login")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> CreateLogin(int id, [FromBody] CreatePersonLoginRequest request)
    {
        var userId = await Mediator.Send(new CreateUserForPersonCommand(id, request.FlatId, request.RoleId, request.Password));
        return Ok(ApiResponse<int>.SuccessResponse(userId, "Login account created."));
    }

    [HttpPost("bulk-create-owner-logins")]
    [HasPermission(Permissions.Occupancy.Manage)]
    public async Task<IActionResult> BulkCreateOwnerLogins(BulkCreateOwnerLoginsCommand command)
    {
        var results = await Mediator.Send(command);
        var createdCount = results.Count(r => r.Created);
        return Ok(ApiResponse<object>.SuccessResponse(results, $"{createdCount} of {results.Count} login(s) created."));
    }
}

public record CreatePersonLoginRequest(int FlatId, int RoleId, string? Password);
