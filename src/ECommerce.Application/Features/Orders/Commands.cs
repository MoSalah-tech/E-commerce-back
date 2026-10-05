using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Application.Mapping;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Features.Orders;

public sealed record PlaceOrderCommand(
    ShippingAddressDto ShippingAddress,
    Domain.Enums.ShippingMethod ShippingMethod = Domain.Enums.ShippingMethod.Standard,
    bool MarkAsPaid = true) : IRequest<Result<OrderDto>>;

/// <summary>
/// Checkout: validates the cart, prices it with current product prices,
/// reserves stock and empties the cart. All of it in one unit of work so a
/// failure never leaves stock deducted from an unsaved order.
/// </summary>
public sealed class PlaceOrderCommandHandler(
    ICustomerRepository customers,
    IProductRepository products,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    IEmailSender emailSender
) : IRequestHandler<PlaceOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<OrderDto>(Error.Unauthorized("You must be signed in to place an order."));

        var customer = await customers.GetWithCartAsync(userId, cancellationToken);

        if (customer is null)
            return Result.Failure<OrderDto>(Error.NotFound("No customer profile is associated with this account."));

        if (!customer.IsActive)
            return Result.Failure<OrderDto>(Error.Forbidden("This account has been deactivated."));

        if (customer.Cart.IsEmpty)
        {
            return Result.Failure<OrderDto>(
                Error.Conflict(ErrorCodes.EmptyCart, "Cannot place an order with an empty cart."));
        }

        var productIds = customer.Cart.Items.Select(x => x.ProductId).ToArray();
        var loaded = await products.GetByIdsAsync(productIds, cancellationToken);
        var catalogue = loaded.ToDictionary(x => x.Id);

        foreach (var line in customer.Cart.Items)
        {
            if (!catalogue.TryGetValue(line.ProductId, out var product))
            {
                return Result.Failure<OrderDto>(
                    Error.NotFound($"Product '{line.ProductId}' is no longer available."));
            }

            if (!product.IsActive)
            {
                return Result.Failure<OrderDto>(
                    Error.Conflict(ErrorCodes.Conflict, $"Product '{product.Name}' is no longer on sale."));
            }

            if (!product.HasSufficientStock(line.Quantity))
            {
                return Result.Failure<OrderDto>(new Error(
                    ErrorCodes.InsufficientStock,
                    $"Only {product.StockQuantity} unit(s) of '{product.Name}' remain in stock.",
                    ErrorType.Conflict));
            }
        }

        // Price the order from the live catalogue, never from the stale cart snapshot.
        var draftLines = customer.Cart.Items
            .Select(line =>
            {
                var product = catalogue[line.ProductId];
                return new OrderDraftLine(product.Id, product.Name, product.Sku, product.EffectivePrice, line.Quantity);
            })
            .ToList();

        var address = ShippingAddress.Create(
            request.ShippingAddress.RecipientName,
            request.ShippingAddress.PhoneNumber,
            request.ShippingAddress.Line1,
            request.ShippingAddress.City,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.Country,
            request.ShippingAddress.Line2,
            request.ShippingAddress.State);

        Order order;
        try
        {
            order = Order.Create(customer.Id, draftLines, address, request.ShippingMethod, orderDate: clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure<OrderDto>(Error.Validation(exception.Message));
        }

        if (request.MarkAsPaid)
            order.MarkAsPaid();

        foreach (var item in order.Items)
            catalogue[item.ProductId].ReserveStock(item.Quantity);

        orders.Add(order);
        customer.Cart.Clear();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendOrderConfirmationAsync(
            customer.Email,
            customer.FullName,
            order.OrderNumber,
            cancellationToken);

        return Result.Success(order.ToDto());
    }
}

public sealed record UpdateOrderStatusCommand(Guid Id, Domain.Enums.OrderStatus Status) : IRequest<Result>;

/// <summary>Administrative status transition. Returns stock to the catalogue when an order is cancelled.</summary>
public sealed class UpdateOrderStatusCommandHandler(
    IOrderRepository orders,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateOrderStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await orders.GetWithItemsAsync(request.Id, cancellationToken);

        if (order is null)
            return Result.Failure(Error.NotFound($"Order '{request.Id}' was not found."));

        try
        {
            switch (request.Status)
            {
                case Domain.Enums.OrderStatus.Confirmed:
                    order.Confirm();
                    break;
                case Domain.Enums.OrderStatus.Shipped:
                    order.Ship();
                    break;
                case Domain.Enums.OrderStatus.Delivered:
                    order.Deliver();
                    break;
                case Domain.Enums.OrderStatus.Cancelled:
                    order.Cancel();
                    break;
                case Domain.Enums.OrderStatus.Refunded:
                    order.Refund();
                    break;
                default:
                    return Result.Failure(
                        Error.Validation($"'{request.Status}' cannot be set directly by an administrator."));
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure(
                new Error(ErrorCodes.InvalidOrderStatusTransition, exception.Message, ErrorType.Validation));
        }

        orders.Update(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed record CancelOrderCommand(Guid Id) : IRequest<Result>;

/// <summary>Cancels a customer's own order and returns the reserved stock.</summary>
public sealed class CancelOrderCommandHandler(
    IOrderRepository orders,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized("You must be signed in to cancel an order."));

        var order = await orders.GetWithItemsAsync(request.Id, cancellationToken);

        if (order is null)
            return Result.Failure(Error.NotFound($"Order '{request.Id}' was not found."));

        var isOwner = order.Customer?.UserId == userId;
        if (!isOwner && !currentUser.IsInRole(Roles.Admin))
            return Result.Failure(Error.Forbidden("You cannot cancel another customer's order."));

        try
        {
            order.Cancel();
        }
        catch (DomainException exception)
        {
            return Result.Failure(
                new Error(ErrorCodes.OrderCannotBeCancelled, exception.Message, ErrorType.Conflict));
        }

        await RestoreStockAsync(order, cancellationToken);

        orders.Update(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task RestoreStockAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToArray();
        var catalogue = (await products.GetByIdsAsync(productIds, cancellationToken)).ToDictionary(x => x.Id);

        foreach (var item in order.Items)
        {
            if (catalogue.TryGetValue(item.ProductId, out var product))
                product.ReleaseStock(item.Quantity);
        }
    }
}