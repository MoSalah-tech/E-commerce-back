using ECommerce.Api.Contracts;
using ECommerce.Api.Extensions;
using ECommerce.Application.Common;
using ECommerce.Application.Features.Orders;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

/// <summary>Checkout and order tracking for the signed-in shopper, plus status management for staff.</summary>
[Authorize]
[Route("api/orders")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] OrderStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new OrderFilter
        {
            Status = status,
            Paging = new PagedRequest(
                pageNumber < 1 ? 1 : pageNumber,
                pageSize < 1 ? PagedRequest.DefaultPageSize : pageSize)
        };

        var result = await mediator.Send(new GetOrdersQuery(filter), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(id), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new PlaceOrderCommand(request.ShippingAddress, request.ShippingMethod, request.MarkAsPaid),
            cancellationToken);

        return result.CreatedAt(this, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateOrderStatusCommand(id, request.Status), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelOrderCommand(id), cancellationToken);
        return result.ToActionResult(this);
    }
}