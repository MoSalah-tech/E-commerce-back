using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using MediatR;

namespace ECommerce.Application.Features.Categories;

public sealed record CreateCategoryCommand(
    string Name,
    string? Description,
    string? ImageUrl,
    int DisplayOrder = 0) : IRequest<Result<Guid>>;

public sealed class CreateCategoryCommandHandler(
    ICategoryRepository categories,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await categories.NameExistsAsync(request.Name, null, cancellationToken))
        {
            return Result.Failure<Guid>(
                Error.Conflict(ErrorCodes.DuplicateCategoryName, $"A category named '{request.Name}' already exists."));
        }

        var category = new Category(request.Name, request.Description, request.ImageUrl, request.DisplayOrder);
        categories.Add(category);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Id);
    }
}

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int DisplayOrder = 0) : IRequest<Result>;

public sealed class UpdateCategoryCommandHandler(
    ICategoryRepository categories,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCategoryCommand, Result>
{
    public async Task<Result> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken);

        if (category is null)
            return Result.Failure(Error.NotFound($"Category '{request.Id}' was not found."));

        if (await categories.NameExistsAsync(request.Name, request.Id, cancellationToken))
        {
            return Result.Failure(
                Error.Conflict(ErrorCodes.DuplicateCategoryName, $"A category named '{request.Name}' already exists."));
        }

        category.UpdateDetails(request.Name, request.Description, request.ImageUrl, request.DisplayOrder);
        categories.Update(category);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteCategoryCommand(Guid Id) : IRequest<Result>;

/// <summary>Soft-deletes a category, refusing while products still reference it.</summary>
public sealed class DeleteCategoryCommandHandler(
    ICategoryRepository categories,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteCategoryCommand, Result>
{
    public async Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken);

        if (category is null)
            return Result.Failure(Error.NotFound($"Category '{request.Id}' was not found."));

        if (await categories.HasProductsAsync(category.Id, cancellationToken))
        {
            return Result.Failure(
                Error.Conflict(
                    ErrorCodes.Conflict,
                    "The category still contains products. Reassign or remove them before deleting it."));
        }

        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}