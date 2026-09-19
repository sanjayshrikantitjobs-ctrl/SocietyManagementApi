using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.API.Authorization;
using SocietyManagement.Application.Features.Inventory;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

[Authorize]
[Route("api/inventory")]
public class InventoryController : ApiControllerBase
{
    [HttpGet("items")]
    [HasPermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<InventoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(
        [FromQuery] int societyId, [FromQuery] string? search, [FromQuery] bool lowStockOnly = false, [FromQuery] bool activeOnly = false,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetInventoryItemsQuery(societyId, search, lowStockOnly, activeOnly, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("items")]
    [HasPermission(Permissions.Inventory.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateItem(CreateInventoryItemCommand command)
    {
        var id = await Mediator.Send(command);
        return Created(string.Empty, ApiResponse<int>.SuccessResponse(id, "Item added."));
    }

    [HttpPut("items/{id:int}")]
    [HasPermission(Permissions.Inventory.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItem(int id, UpdateInventoryItemCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse.FailureResponse("Route id does not match payload id."));
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Item updated."));
    }

    [HttpDelete("items/{id:int}")]
    [HasPermission(Permissions.Inventory.Manage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteItem(int id)
    {
        await Mediator.Send(new DeleteInventoryItemCommand(id));
        return Ok(ApiResponse.SuccessResponse("Item removed."));
    }

    [HttpGet("transactions")]
    [HasPermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<StockTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int societyId, [FromQuery] int? inventoryItemId, [FromQuery] StockTransactionType? type,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = AppConstants.DefaultPageSize)
    {
        var result = await Mediator.Send(new GetStockTransactionsQuery(societyId, inventoryItemId, type, from, to, pageNumber, pageSize));
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("transactions")]
    [HasPermission(Permissions.Inventory.Manage)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordTransaction(RecordStockTransactionCommand command)
    {
        var id = await Mediator.Send(command);
        return Created(string.Empty, ApiResponse<int>.SuccessResponse(id, "Stock updated."));
    }
}
