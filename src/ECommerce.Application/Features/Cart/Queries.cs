using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Mapping;
using MediatR;

namespace ECommerce.Application.Features.Cart;

public sealed record GetCartQuery : IRequest<Result<CartDto>>;

public sealed class GetCartQueryHandler(
    ICustomerRepository customers,
    ICurrentUserService currentUser) : IRequestHandler<GetCartQuery, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<CartDto>(Error.Unauthorized("You must be signed in to view your cart."));

        var customer = await customers.GetWithCartAsync(userId, cancellationToken);

        if (customer is null)
            return Result.Failure<CartDto>(Error.NotFound("No customer profile is associated with this account."));

        return Result.Success(customer.Cart.ToDto());
    }
}