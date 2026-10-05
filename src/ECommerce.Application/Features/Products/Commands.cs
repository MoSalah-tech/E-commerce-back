using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Features.Products;

public sealed record CreateProductCommand(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    Guid CategoryId,
    int StockQuantity = 0,
    int LowStockThreshold = 5,
    decimal? CompareAtPrice = null,
    string? ImageUrl = null,
    bool IsFeatured = false) : IRequest<Result<Guid>>;

public sealed class CreateProductCommandHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (!await categories.ExistsAsync(request.CategoryId, cancellationToken))
            return Result.Failure<Guid>(Error.NotFound($"Category '{request.CategoryId}' was not found."));

        if (await products.SkuExistsAsync(request.Sku, null, cancellationToken))
        {
            return Result.Failure<Guid>(
                Error.Conflict(ErrorCodes.DuplicateSku, $"A product with SKU '{request.Sku}' already exists."));
        }

        Product product;

        try
        {
            product = new Product(
                request.Name,
                request.Sku,
                new Money(request.Price),
                request.CategoryId,
                request.Description,
                request.ImageUrl,
                request.StockQuantity);

            product.SetLowStockThreshold(request.LowStockThreshold);
            product.SetFeatured(request.IsFeatured);
            product.SetCompareAtPrice(request.CompareAtPrice.HasValue ? new Money(request.CompareAtPrice.Value) : null);
        }
        catch (DomainException exception)
        {
            return Result.Failure<Guid>(Error.Validation(exception.Message));
        }

        products.Add(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(product.Id);
    }
}

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    decimal? CompareAtPrice = null,
    string? ImageUrl = null,
    bool IsFeatured = false) : IRequest<Result>;

public sealed class UpdateProductCommandHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateProductCommand, Result>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(Error.NotFound($"Product '{request.Id}' was not found."));

        if (!await categories.ExistsAsync(request.CategoryId, cancellationToken))
            return Result.Failure(Error.NotFound($"Category '{request.CategoryId}' was not found."));

        try
        {
            product.UpdateDetails(
                request.Name,
                request.Description,
                new Money(request.Price),
                request.ImageUrl,
                request.CategoryId,
                request.IsFeatured);

            product.SetCompareAtPrice(request.CompareAtPrice.HasValue ? new Money(request.CompareAtPrice.Value) : null);
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Validation(exception.Message));
        }

        products.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed record DeleteProductCommand(Guid Id) : IRequest<Result>;

/// <summary>Soft-deletes a product. Historical order lines keep their own snapshots.</summary>
public sealed class DeleteProductCommandHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(Error.NotFound($"Product '{request.Id}' was not found."));

        products.Remove(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed record AdjustStockCommand(Guid Id, int QuantityDelta, string? Reason = null) : IRequest<Result>;

public sealed class AdjustStockCommandHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork) : IRequestHandler<AdjustStockCommand, Result>
{
    public async Task<Result> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(Error.NotFound($"Product '{request.Id}' was not found."));

        if (request.QuantityDelta > 0)
            product.Restock(request.QuantityDelta);
        else if (request.QuantityDelta < 0)
            product.ReserveStock(-request.QuantityDelta);
        else
            return Result.Failure(Error.Validation("Quantity delta cannot be zero."));

        products.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed record SetDiscountCommand(Guid Id, decimal Percentage) : IRequest<Result>;

public sealed class SetDiscountCommandHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork) : IRequestHandler<SetDiscountCommand, Result>
{
    public async Task<Result> Handle(SetDiscountCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(Error.NotFound($"Product '{request.Id}' was not found."));

        if (request.Percentage is < 0 or > 100)
        {
            return Result.Failure(
                Error.Validation($"Discount percentage must be between 0 and 100, but was {request.Percentage}."));
        }

        product.SetDiscount(request.Percentage);
        products.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}