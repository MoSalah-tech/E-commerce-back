using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces;

/// <summary>A category paired with its product count, produced by a single grouped query.</summary>
public sealed record CategorySummary(Category Category, int ProductCount);

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorySummary>> ListWithCountsAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>True when the category still has products and therefore cannot be deleted.</summary>
    Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<int> CountProductsAsync(Guid categoryId, CancellationToken cancellationToken = default);
}