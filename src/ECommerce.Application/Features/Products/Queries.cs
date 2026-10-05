using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Mapping;
using MediatR;

namespace ECommerce.Application.Features.Products;

public sealed record GetProductsQuery(ProductFilter Filter) : IRequest<Result<PagedResult<ProductDto>>>;

public sealed class GetProductsQueryHandler(IProductRepository products)
    : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductDto>>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await products.GetPagedAsync(request.Filter, cancellationToken);
        return Result.Success(page.Map(x => x.ToDto()));
    }
}

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;

public sealed class GetProductByIdQueryHandler(IProductRepository products)
    : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.Id, cancellationToken);

        return product is null
            ? Result.Failure<ProductDto>(Error.NotFound($"Product '{request.Id}' was not found."))
            : Result.Success(product.ToDto());
    }
}

public sealed record GetFeaturedProductsQuery(int Count = 8) : IRequest<Result<IReadOnlyList<ProductDto>>>;

public sealed class GetFeaturedProductsQueryHandler(IProductRepository products)
    : IRequestHandler<GetFeaturedProductsQuery, Result<IReadOnlyList<ProductDto>>>
{
    public async Task<Result<IReadOnlyList<ProductDto>>> Handle(
        GetFeaturedProductsQuery request,
        CancellationToken cancellationToken)
    {
        var filter = new ProductFilter
        {
            FeaturedOnly = true,
            InStockOnly = true,
            Paging = new PagedRequest(1, Math.Clamp(request.Count, 1, PagedRequest.MaxPageSize))
        };

        var page = await products.GetPagedAsync(filter, cancellationToken);
        var dtos = page.Items.Select(x => x.ToDto()).ToList();

        return Result.Success<IReadOnlyList<ProductDto>>(dtos);
    }
}