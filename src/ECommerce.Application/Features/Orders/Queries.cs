using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Mapping;
using MediatR;

namespace ECommerce.Application.Features.Orders;

public sealed record GetOrdersQuery(OrderFilter Filter) : IRequest<Result<PagedResult<OrderDto>>>;

/// <summary>Returns the caller's orders; administrators see every order in the shop.</summary>
public sealed class GetOrdersQueryHandler(
    IOrderRepository orders,
    ICurrentUserService currentUser) : IRequestHandler<GetOrdersQuery, Result<PagedResult<OrderDto>>>
{
    public async Task<Result<PagedResult<OrderDto>>> Handle(
        GetOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<PagedResult<OrderDto>>(Error.Unauthorized("You must be signed in to view orders."));

        var filter = currentUser.IsInRole(Roles.Admin)
            ? request.Filter
            : request.Filter with { UserId = userId };

        var page = await orders.GetPagedAsync(filter, cancellationToken);

        return Result.Success(page.Map(x => x.ToDto()));
    }
}

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<Result<OrderDto>>;

public sealed class GetOrderByIdQueryHandler(
    IOrderRepository orders,
    ICurrentUserService currentUser) : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await orders.GetWithItemsAsync(request.Id, cancellationToken);

        if (order is null)
            return Result.Failure<OrderDto>(Error.NotFound($"Order '{request.Id}' was not found."));

        var isOwner = currentUser.UserId is Guid userId && order.Customer?.UserId == userId;

        if (!isOwner && !currentUser.IsInRole(Roles.Admin))
            return Result.Failure<OrderDto>(Error.Forbidden("You cannot view another customer's order."));

        return Result.Success(order.ToDto());
    }
}