using FluentValidation;

namespace ECommerce.Application.Features.Orders;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.ShippingAddress).NotNull().WithMessage("Shipping address is required.");

        When(x => x.ShippingAddress is not null, () =>
        {
            RuleFor(x => x.ShippingAddress.RecipientName)
                .NotEmpty().WithMessage("Recipient name is required.")
                .MaximumLength(120).WithMessage("Recipient name cannot exceed 120 characters.");

            RuleFor(x => x.ShippingAddress.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches("^[0-9+()\\- ]{6,20}$").WithMessage("Phone number format is invalid.");

            RuleFor(x => x.ShippingAddress.Line1)
                .NotEmpty().WithMessage("Address line 1 is required.")
                .MaximumLength(200).WithMessage("Address line 1 cannot exceed 200 characters.");

            RuleFor(x => x.ShippingAddress.Line2)
                .MaximumLength(200).WithMessage("Address line 2 cannot exceed 200 characters.")
                .When(x => !string.IsNullOrWhiteSpace(x.ShippingAddress.Line2));

            RuleFor(x => x.ShippingAddress.City)
                .NotEmpty().WithMessage("City is required.")
                .MaximumLength(100).WithMessage("City cannot exceed 100 characters.");

            RuleFor(x => x.ShippingAddress.PostalCode)
                .NotEmpty().WithMessage("Postal code is required.")
                .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters.");

            RuleFor(x => x.ShippingAddress.Country)
                .NotEmpty().WithMessage("Country is required.")
                .MaximumLength(100).WithMessage("Country cannot exceed 100 characters.");
        });
    }
}

public sealed class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator() =>
        RuleFor(x => x.Id).NotEmpty().WithMessage("Order id is required.");
}

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator() =>
        RuleFor(x => x.Id).NotEmpty().WithMessage("Order id is required.");
}