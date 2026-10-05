using ECommerce.Application.Common;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<PagedResult<Product>> GetPagedAsync(ProductFilter filter, CancellationToken cancellationToken = default);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Loads every product that still has at least one review, for rating recalculation.</summary>
    Task<IReadOnlyList<Product>> GetReviewedAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
}