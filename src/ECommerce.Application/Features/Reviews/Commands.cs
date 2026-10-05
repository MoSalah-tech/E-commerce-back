using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Application.Mapping;
using ECommerce.Domain.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Reviews;

/// <summary>
/// Recomputes and persists a product's cached rating. Shared by every command
/// that can change the set of approved reviews.
/// </summary>
internal static class ReviewRatingRefresher
{
    public static async Task RefreshAsync(
        IReviewRepository reviews,
        IProductRepository products,
        IUnitOfWork unitOfWork,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(productId, cancellationToken);

        if (product is null)
            return;

        var (average, count) = await reviews.GetRatingSummaryAsync(productId, cancellationToken);
        product.RecalculateRating(average, count);

        products.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CreateReviewCommand(
    Guid ProductId,
    int Rating,
    string? Title = null,
    string? Comment = null) : IRequest<Result<Guid>>;

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews,
    IProductRepository products,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<CreateReviewCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<Guid>(Error.Unauthorized("You must be signed in to write a review."));

        var customer = await customers.GetByUserIdAsync(userId, cancellationToken);

        if (customer is null)
            return Result.Failure<Guid>(Error.NotFound("No customer profile is associated with this account."));

        var product = await products.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null)
            return Result.Failure<Guid>(Error.NotFound($"Product '{request.ProductId}' was not found."));

        var existing = await reviews.GetByProductAndCustomerAsync(product.Id, customer.Id, cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<Guid>(
                Error.Conflict(ErrorCodes.ReviewAlreadyExists, "You have already reviewed this product."));
        }

        Review review;
        try
        {
            review = new Review(product.Id, customer.Id, request.Rating, request.Title, request.Comment);
        }
        catch (DomainException exception)
        {
            return Result.Failure<Guid>(Error.Validation(exception.Message));
        }

        reviews.Add(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ReviewRatingRefresher.RefreshAsync(reviews, products, unitOfWork, product.Id, cancellationToken);

        return Result.Success(review.Id);
    }
}

public sealed record UpdateReviewCommand(
    Guid Id,
    int Rating,
    string? Title = null,
    string? Comment = null) : IRequest<Result>;

public sealed class UpdateReviewCommandHandler(
    IReviewRepository reviews,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<UpdateReviewCommand, Result>
{
    public async Task<Result> Handle(UpdateReviewCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized("You must be signed in to edit a review."));

        var review = await reviews.GetByIdAsync(request.Id, cancellationToken);

        if (review is null)
            return Result.Failure(Error.NotFound($"Review '{request.Id}' was not found."));

        if (review.Customer?.UserId != userId && !currentUser.IsInRole(Roles.Admin))
            return Result.Failure(Error.Forbidden("You can only edit your own reviews."));

        try
        {
            review.Edit(request.Rating, request.Title, request.Comment);
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Validation(exception.Message));
        }

        reviews.Update(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ReviewRatingRefresher.RefreshAsync(reviews, products, unitOfWork, review.ProductId, cancellationToken);

        return Result.Success();
    }
}

public sealed record DeleteReviewCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteReviewCommandHandler(
    IReviewRepository reviews,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<DeleteReviewCommand, Result>
{
    public async Task<Result> Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized("You must be signed in to delete a review."));

        var review = await reviews.GetByIdAsync(request.Id, cancellationToken);

        if (review is null)
            return Result.Failure(Error.NotFound($"Review '{request.Id}' was not found."));

        if (review.Customer?.UserId != userId && !currentUser.IsInRole(Roles.Admin))
            return Result.Failure(Error.Forbidden("You can only delete your own reviews."));

        var productId = review.ProductId;

        reviews.Remove(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ReviewRatingRefresher.RefreshAsync(reviews, products, unitOfWork, productId, cancellationToken);

        return Result.Success();
    }
}

public sealed record ModerateReviewCommand(Guid Id, bool IsApproved) : IRequest<Result>;

public sealed class ModerateReviewCommandHandler(
    IReviewRepository reviews,
    IProductRepository products,
    IUnitOfWork unitOfWork) : IRequestHandler<ModerateReviewCommand, Result>
{
    public async Task<Result> Handle(ModerateReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(request.Id, cancellationToken);

        if (review is null)
            return Result.Failure(Error.NotFound($"Review '{request.Id}' was not found."));

        if (request.IsApproved)
            review.Approve();
        else
            review.Reject();

        reviews.Update(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ReviewRatingRefresher.RefreshAsync(reviews, products, unitOfWork, review.ProductId, cancellationToken);

        return Result.Success();
    }
}