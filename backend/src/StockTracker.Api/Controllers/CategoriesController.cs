using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Catalog.Categories;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CategoryDto>> List([FromQuery] GetCategoriesQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        var id = await sender.Send(new CreateCategoryCommand(request.Name), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CreatedResponse(id));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> Update(Guid id, CategoryRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateCategoryCommand(id, request.Name), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}
