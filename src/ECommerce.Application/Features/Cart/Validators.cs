using FluentValidation;

namespace ECommerce.Application.Features.Cart;

public sealed class AddToCartCommandValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product id is required.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 100).WithMessage("Quantity must be between 1 and 100.");
    }
}

public sealed class UpdateCartItemCommandValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product id is required.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(0, 100).WithMessage("Quantity must be between 0 and 100. Send 0 to remove the item.");
    }
}

public sealed class RemoveFromCartCommandValidator : AbstractValidator<RemoveFromCartCommand>
{
    public RemoveFromCartCommandValidator() =>
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product id is required.");
}