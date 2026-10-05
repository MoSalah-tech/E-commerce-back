using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Mapping;
using ECommerce.Domain.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Cart;

public sealed record AddToCartCommand(Guid ProductId, int Quantity = 1) : IRequest<Result<CartDto>>;

public sealed class AddToCartCommandHandler(
    ICustomerRepository customers,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser
) : IRequestHandler<AddToCartCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<CartDto>(Error.Unauthorized("You must be signed in to modify your cart."));

        var product = await products.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null)
            return Result.Failure<CartDto>(Error.NotFound($"Product '{request.ProductId}' was not found."));

        var customer = await customers.GetWithCartAsync(userId, cancellationToken);

        if (customer is null)
            return Result.Failure<CartDto>(Error.NotFound("No customer profile is associated with this account."));

        try
        {
            customer.Cart.AddItem(product, request.Quantity);
        }
        catch (DomainException exception)
        {
            return Result.Failure<CartDto>(Error.Validation(exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(customer.Cart.ToDto());
    }
}

public sealed record UpdateCartItemCommand(Guid ProductId, int Quantity) : IRequest<Result<CartDto>>;

public sealed class UpdateCartItemCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser
) : IRequestHandler<UpdateCartItemCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<CartDto>(Error.Unauthorized("You must be signed in to modify your cart."));

        var customer = await customers.GetWithCartAsync(userId, cancellationToken);

        if (customer is null)
            return Result.Failure<CartDto>(Error.NotFound("No customer profile is associated with this account."));

        try
        {
            customer.Cart.UpdateItemQuantity(request.ProductId, request.Quantity);
        }
        catch (DomainException exception)
        {
            return Result.Failure<CartDto>(Error.NotFound(exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(customer.Cart.ToDto());
    }
}

public sealed record RemoveFromCartCommand(Guid ProductId) : IRequest<Result<CartDto>>;

public sealed class RemoveFromCartCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser
) : IRequestHandler<RemoveFromCartCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(RemoveFromCartCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<CartDto>(Error.Unauthorized("You must be signed in to modify your cart."));

        var customer = await customers.GetWithCartAsync(userId, cancellationToken);

        if (customer is null)
            return Result.Failure<CartDto>(Error.NotFound("No customer profile is associated with this account."));

        try
        {
            customer.Cart.RemoveItem(request.ProductId);
        }
        catch (DomainException exception)
        {
            return Result.Failure<CartDto>(Error.NotFound(exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(customer.Cart.ToDto());
    }
}

public sealed record ClearCartCommand : IRequest<Result>;

public sealed class ClearCartCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<ClearCartCommand, Result>
{
    public async Task<Result> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized("You must be signed in to modify your cart."));

        var customer = await customers.GetWithCartAsync(userId, cancellationToken);

        if (customer is null)
            return Result.Failure(Error.NotFound("No customer profile is associated with this account."));

        customer.Cart.Clear();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}