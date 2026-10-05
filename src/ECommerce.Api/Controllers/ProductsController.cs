using ECommerce.Api.Contracts;
using ECommerce.Api.Extensions;
using ECommerce.Application.Common;
using ECommerce.Application.Features.Products;
using ECommerce.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

/// <summary>Catalogue browsing (public) and catalogue administration (Admin).</summary>
[Route("api/products")]
public sealed class ProductsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] bool inStockOnly = false,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new ProductFilter
        {
            CategoryId = categoryId,
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            InStockOnly = inStockOnly,
            ActiveOnly = !includeInactive,
            SortBy = sortBy,
            SortDescending = sortDescending,
            Paging = SanitizePaging(pageNumber, pageSize)
        };

        var result = await mediator.Send(new GetProductsQuery(filter), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("featured")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatured([FromQuery] int count = 8, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetFeaturedProductsQuery(count), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateProductCommand(
                request.Name,
                request.Description,
                request.Sku,
                request.Price,
                request.CategoryId,
                request.StockQuantity,
                request.LowStockThreshold,
                request.CompareAtPrice,
                request.ImageUrl,
                request.IsFeatured),
            cancellationToken);

        return result.CreatedAt(this, nameof(GetById), new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateProductCommand(
                id,
                request.Name,
                request.Description,
                request.Price,
                request.CategoryId,
                request.CompareAtPrice,
                request.ImageUrl,
                request.IsFeatured),
            cancellationToken);

        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteProductCommand(id), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/stock")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjustStock(Guid id, [FromBody] AdjustStockRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AdjustStockCommand(id, request.QuantityDelta, request.Reason),
            cancellationToken);

        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/discount")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetDiscount(Guid id, [FromBody] SetDiscountRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetDiscountCommand(id, request.Percentage), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Query strings are user input, so paging is clamped instead of trusted.</summary>
    private static PagedRequest SanitizePaging(int pageNumber, int pageSize) =>
        new(pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? PagedRequest.DefaultPageSize : pageSize);
}