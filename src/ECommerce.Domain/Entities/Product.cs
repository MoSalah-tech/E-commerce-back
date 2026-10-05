using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

/// <summary>A sellable item. Aggregate root and the most behaviour-heavy entity in the model.</summary>
public class Product : Entity, IAggregateRoot
{
    /// <summary>Hard ceiling on a single cart line, protecting against accidental bulk orders.</summary>
    public const int MaxQuantityPerOrder = 100;

    private readonly List<Review> _reviews = [];

    private Product()
    {
    }

    public Product(
        string name,
        string sku,
        Money price,
        Guid categoryId,
        string? description = null,
        string? imageUrl = null,
        int stockQuantity = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        if (price.Amount < 0)
            throw new DomainException("Product price cannot be negative.");

        if (categoryId == Guid.Empty)
            throw new DomainException("A product must belong to a category.");

        if (stockQuantity < 0)
            throw new DomainException("Stock quantity cannot be negative.");

        Name = Validate(name).Trim();
        Sku = sku.Trim().ToUpperInvariant();
        Price = price;
        Description = description?.Trim() ?? string.Empty;
        ImageUrl = imageUrl?.Trim();
        StockQuantity = stockQuantity;
        LowStockThreshold = 5;
        CategoryId = categoryId;
        IsActive = true;
        DiscountPercentage = 0m;
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public Money Price { get; private set; } = Money.Zero();

    /// <summary>Optional strike-through reference price shown next to <see cref="Price"/>.</summary>
    public Money? CompareAtPrice { get; private set; }

    public decimal DiscountPercentage { get; private set; }

    public int StockQuantity { get; private set; }

    public int LowStockThreshold { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsFeatured { get; private set; }

    public string? ImageUrl { get; private set; }

    public decimal AverageRating { get; private set; }

    public int ReviewCount { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category? Category { get; private set; }

    public IReadOnlyCollection<Review> Reviews => _reviews.AsReadOnly();

    /// <summary>Price the customer actually pays after any percentage discount.</summary>
    public Money EffectivePrice => Price.ApplyPercentageDiscount(DiscountPercentage);

    /// <summary>
    /// True when the customer pays less than the list price, either because a
    /// percentage discount is applied or because the compare-at price is higher.
    /// </summary>
    public bool IsOnSale => DiscountPercentage > 0m || (CompareAtPrice is not null && CompareAtPrice > Price);

    public bool IsInStock => StockQuantity > 0;

    public bool IsLowOnStock => StockQuantity > 0 && StockQuantity <= LowStockThreshold;

    public bool HasSufficientStock(int quantity) => quantity > 0 && StockQuantity >= quantity;

    public void UpdateDetails(
        string name,
        string? description,
        Money price,
        string? imageUrl,
        Guid categoryId,
        bool isFeatured = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (price.Amount < 0)
            throw new DomainException("Product price cannot be negative.");

        if (categoryId == Guid.Empty)
            throw new DomainException("A product must belong to a category.");

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Price = price;
        ImageUrl = imageUrl?.Trim();
        CategoryId = categoryId;
        IsFeatured = isFeatured;
        Touch();
    }

    public void ChangeSku(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        Sku = sku.Trim().ToUpperInvariant();
        Touch();
    }

    public void SetCompareAtPrice(Money? compareAtPrice)
    {
        if (compareAtPrice is not null && compareAtPrice.Amount > 0 && compareAtPrice < Price)
        {
            throw new DomainException("The compare-at price must be greater than or equal to the selling price.");
        }

        CompareAtPrice = compareAtPrice is { Amount: 0 } ? null : compareAtPrice;
        Touch();
    }

    public void SetDiscount(decimal percentage)
    {
        if (percentage is < 0 or > 100)
            throw new DomainException($"Discount percentage must be between 0 and 100, but was {percentage}.");

        DiscountPercentage = percentage;
        Touch();
    }

    public void SetLowStockThreshold(int threshold)
    {
        if (threshold < 0)
            throw new DomainException("Low stock threshold cannot be negative.");

        LowStockThreshold = threshold;
        Touch();
    }

    public void SetFeatured(bool isFeatured)
    {
        IsFeatured = isFeatured;
        Touch();
    }

    /// <summary>Adds stock, e.g. after a restock or a customer return.</summary>
    public void Restock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Restock quantity must be greater than zero.");

        StockQuantity += quantity;
        Touch();
    }

    /// <summary>
    /// Decrements stock when an order is placed. Throws
    /// <see cref="InsufficientStockException"/> when the warehouse cannot fulfil the request.
    /// </summary>
    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Reservation quantity must be greater than zero.");

        if (quantity > StockQuantity)
            throw new InsufficientStockException(Name, quantity, StockQuantity);

        StockQuantity -= quantity;
        Touch();
    }

    /// <summary>Puts stock back, e.g. when an order is cancelled or refunded.</summary>
    public void ReleaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Release quantity must be greater than zero.");

        StockQuantity += quantity;
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

    /// <summary>Recomputes the cached rating after a review is added, edited or removed.</summary>
    public void RecalculateRating(decimal averageRating, int reviewCount)
    {
        if (reviewCount < 0)
            throw new DomainException("Review count cannot be negative.");

        if (reviewCount > 0 && averageRating is < 0 or > 5)
            throw new DomainException("Average rating must be between 0 and 5.");

        AverageRating = Math.Round(averageRating, 2, MidpointRounding.ToEven);
        ReviewCount = reviewCount;
        Touch();
    }

    private static string Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");

        return name;
    }
}