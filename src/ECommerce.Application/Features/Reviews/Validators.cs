using FluentValidation;

namespace ECommerce.Application.Features.Reviews;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product id is required.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");

        RuleFor(x => x.Title)
            .MaximumLength(150).WithMessage("Title cannot exceed 150 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Title));

        RuleFor(x => x.Comment)
            .MaximumLength(2000).WithMessage("Comment cannot exceed 2000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Comment));
    }
}

public sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Review id is required.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");

        RuleFor(x => x.Title)
            .MaximumLength(150).WithMessage("Title cannot exceed 150 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Title));
    }
}

public sealed class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator() =>
        RuleFor(x => x.Id).NotEmpty().WithMessage("Review id is required.");
}

public sealed class ModerateReviewCommandValidator : AbstractValidator<ModerateReviewCommand>
{
    public ModerateReviewCommandValidator() => RuleFor(x => x.Id).NotEmpty().WithMessage("Review id is required.");
}