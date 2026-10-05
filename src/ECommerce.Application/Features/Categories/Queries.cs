using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Mapping;
using MediatR;

namespace ECommerce.Application.Features.Categories;

public sealed record GetCategoriesQuery(bool ActiveOnly = true) : IRequest<Result<IReadOnlyList<CategoryDto>>>;

public sealed class GetCategoriesQueryHandler(ICategoryRepository categories)
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var summaries = await categories.ListWithCountsAsync(request.ActiveOnly, cancellationToken);

        var dtos = new List<CategoryDto>(summaries.Count);
        foreach (var summary in summaries)
        {
            var dto = summary.Category.ToDto();
            dto.ProductCount = summary.ProductCount;
            dtos.Add(dto);
        }

        return Result.Success<IReadOnlyList<CategoryDto>>(dtos);
    }
}

public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<Result<CategoryDto>>;

public sealed class GetCategoryByIdQueryHandler(ICategoryRepository categories)
    : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken);

        if (category is null)
            return Result.Failure<CategoryDto>(Error.NotFound($"Category '{request.Id}' was not found."));

        var dto = category.ToDto();
        dto.ProductCount = await categories.CountProductsAsync(category.Id, cancellationToken);

        return Result.Success(dto);
    }
}