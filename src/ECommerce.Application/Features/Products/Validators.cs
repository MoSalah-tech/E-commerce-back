using FluentValidation;

namespace ECommerce.Application.Features.Products;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MinimumLength(2).WithMessage("Product name must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters.")
            .Matches("^[A-Za-z0-9-]+$").WithMessage("SKU may only contain letters, numbers and hyphens.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Price is unrealistically high.");

        RuleFor(x => x.CompareAtPrice)
            .GreaterThanOrEqualTo(x => x.Price).WithMessage("Compare-at price must be greater than or equal to the price.")
            .When(x => x.CompareAtPrice.HasValue);

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative.");

        RuleFor(x => x.LowStockThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("Low stock threshold cannot be negative.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required.");

        RuleFor(x => x.Description)
            .MaximumLength(4000).WithMessage("Description cannot exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MinimumLength(2).WithMessage("Product name must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Price is unrealistically high.");

        RuleFor(x => x.CompareAtPrice)
            .GreaterThanOrEqualTo(x => x.Price).WithMessage("Compare-at price must be greater than or equal to the price.")
            .When(x => x.CompareAtPrice.HasValue);

        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Category is required.");

        RuleFor(x => x.Description)
            .MaximumLength(4000).WithMessage("Description cannot exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator() =>
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product id is required.");
}

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product id is required.");

        RuleFor(x => x.QuantityDelta)
            .NotEqual(0).WithMessage("Quantity delta cannot be zero.")
            .InclusiveBetween(-100_000, 100_000).WithMessage("Quantity delta is out of range.");
    }
}

public sealed class SetDiscountCommandValidator : AbstractValidator<SetDiscountCommand>
{
    public SetDiscountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product id is required.");

        RuleFor(x => x.Percentage)
            .InclusiveBetween(0m, 100m).WithMessage("Discount percentage must be between 0 and 100.");
    }
}