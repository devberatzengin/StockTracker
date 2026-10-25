using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Catalog.Products;
using StockTracker.Application.Inventory;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductListItemDto>> List(
        [FromQuery] GetProductsQuery query,
        CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetProductQuery(id), cancellationToken);

    [HttpGet("{id:guid}/stock")]
    public Task<ProductStockDto> Stock(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetProductStockQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CreatedResponse(id));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateProductCommand(
                id,
                request.Name,
                request.Description,
                request.CategoryId,
                request.Unit,
                request.TrackingType,
                request.IsAssignable,
                request.MinStockLevel),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateProductCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/barcodes")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddBarcode(Guid id, AddBarcodeRequest request, CancellationToken cancellationToken)
    {
        var barcodeId = await sender.Send(
            new AddProductBarcodeCommand(id, request.Value, request.Format),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CreatedResponse(barcodeId));
    }

    [HttpDelete("{id:guid}/barcodes/{barcodeId:guid}")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> RemoveBarcode(Guid id, Guid barcodeId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveProductBarcodeCommand(id, barcodeId), cancellationToken);
        return NoContent();
    }
}
