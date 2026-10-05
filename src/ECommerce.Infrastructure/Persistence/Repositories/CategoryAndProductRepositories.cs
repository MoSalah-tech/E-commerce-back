using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(AppDbContext context)
    : RepositoryBase<Category>(context), ICategoryRepository
{
    public Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Entities.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<CategorySummary>> ListWithCountsAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = Entities.AsNoTracking();

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategorySummary(x, x.Products.Count))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        Entities.AnyAsync(
            x => x.Name == name && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        Entities.AnyAsync(x => x.Id == categoryId && x.Products.Any(), cancellationToken);

    public Task<int> CountProductsAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        Entities
            .Where(x => x.Id == categoryId)
            .Select(x => x.Products.Count)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class ProductRepository(AppDbContext context)
    : RepositoryBase<Product>(context), IProductRepository
{
    public override async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Entities
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedResult<Product>> GetPagedAsync(
        ProductFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(filter);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ApplySorting(query, filter)
            .Skip(filter.Paging.Skip)
            .Take(filter.Paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, totalCount, filter.Paging.PageNumber, filter.Paging.PageSize);
    }

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        Entities.FirstOrDefaultAsync(x => x.Sku == sku, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken = default) =>
        Entities.AnyAsync(
            x => x.Sku == sku && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        var idList = ids.ToArray();
        return await Entities.Where(x => idList.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetReviewedAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return [];

        var idList = productIds.ToArray();
        return await Entities.Where(x => idList.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    private IQueryable<Product> BuildQuery(ProductFilter filter)
    {
        var query = Entities.Include(x => x.Category).AsQueryable();

        if (filter.ActiveOnly)
            query = query.Where(x => x.IsActive);

        if (filter.InStockOnly)
            query = query.Where(x => x.StockQuantity > 0);

        if (filter.FeaturedOnly)
            query = query.Where(x => x.IsFeatured);

        if (filter.CategoryId.HasValue)
            query = query.Where(x => x.CategoryId == filter.CategoryId.Value);

        if (filter.MinPrice.HasValue)
            query = query.Where(x => x.Price.Amount >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query = query.Where(x => x.Price.Amount <= filter.MaxPrice.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Description.Contains(term) || x.Sku.Contains(term));
        }

        return query;
    }

    private static IQueryable<Product> ApplySorting(IQueryable<Product> query, ProductFilter filter) =>
        filter.SortBy?.ToLowerInvariant() switch
        {
            ProductFilter.SortByPrice => filter.SortDescending
                ? query.OrderByDescending(x => x.Price.Amount).ThenBy(x => x.Name)
                : query.OrderBy(x => x.Price.Amount).ThenBy(x => x.Name),
            ProductFilter.SortByCreated => filter.SortDescending
                ? query.OrderByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.CreatedAt),
            ProductFilter.SortByRating => filter.SortDescending
                ? query.OrderByDescending(x => x.AverageRating).ThenBy(x => x.Name)
                : query.OrderBy(x => x.AverageRating).ThenBy(x => x.Name),
            _ => filter.SortDescending
                ? query.OrderByDescending(x => x.Name)
                : query.OrderBy(x => x.Name)
        };
}