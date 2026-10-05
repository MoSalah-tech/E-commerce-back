namespace ECommerce.Application.Features.Products;

public sealed class ProductDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal EffectivePrice { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public decimal DiscountPercentage { get; set; }

    public string Currency { get; set; } = "USD";

    public bool IsOnSale { get; set; }

    public int StockQuantity { get; set; }

    public bool IsInStock { get; set; }

    public bool IsLowOnStock { get; set; }

    public bool IsActive { get; set; }

    public bool IsFeatured { get; set; }

    public string? ImageUrl { get; set; }

    public decimal AverageRating { get; set; }

    public int ReviewCount { get; set; }

    public Guid CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int StockQuantity { get; set; }

    public int LowStockThreshold { get; set; } = 5;

    public Guid CategoryId { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsFeatured { get; set; }
}

public sealed class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public Guid CategoryId { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsFeatured { get; set; }
}

public sealed class AdjustStockRequest
{
    /// <summary>Positive to restock, negative to remove stock.</summary>
    public int QuantityDelta { get; set; }

    public string? Reason { get; set; }
}

public sealed class SetDiscountRequest
{
    public decimal Percentage { get; set; }
}