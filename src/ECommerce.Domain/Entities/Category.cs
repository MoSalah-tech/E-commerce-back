using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

/// <summary>Product grouping. Aggregate root.</summary>
public class Category : Entity, IAggregateRoot
{
    private readonly List<Product> _products = [];

    private Category()
    {
    }

    public Category(string name, string? description = null, string? imageUrl = null, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required.");

        Name = name.Trim();
        Slug = SlugGenerator.From(Name);
        Description = description?.Trim();
        ImageUrl = imageUrl?.Trim();
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? ImageUrl { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    public void UpdateDetails(string name, string? description, string? imageUrl, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required.");

        Name = name.Trim();
        Slug = SlugGenerator.From(Name);
        Description = description?.Trim();
        ImageUrl = imageUrl?.Trim();
        DisplayOrder = displayOrder;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}