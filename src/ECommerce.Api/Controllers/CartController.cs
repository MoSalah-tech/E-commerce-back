using ECommerce.Api.Contracts;
using ECommerce.Api.Extensions;
using ECommerce.Application.Features.Cart;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

/// <summary>The signed-in shopper's cart. Every route requires authentication.</summary>
[Authorize]
[Route("api/cart")]
public sealed class CartController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCartQuery(), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddItem([FromBody] AddToCartRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddToCartCommand(request.ProductId, request.Quantity), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("items/{productId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItem(
        Guid productId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateCartItemCommand(productId, request.Quantity), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItem(Guid productId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveFromCartCommand(productId), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ClearCartCommand(), cancellationToken);
        return result.ToActionResult(this);
    }
}