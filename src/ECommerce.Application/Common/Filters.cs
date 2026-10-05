namespace ECommerce.Application.Common;

/// <summary>Catalogue filtering, sorting and paging criteria for the product list query.</summary>
public sealed record ProductFilter
{
    public const string SortByName = "name";
    public const string SortByPrice = "price";
    public const string SortByCreated = "created";
    public const string SortByRating = "rating";

    public string? Search { get; init; }

    public Guid? CategoryId { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public bool InStockOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    public bool ActiveOnly { get; init; } = true;

    public string? SortBy { get; init; } = SortByName;

    public bool SortDescending { get; init; }

    public PagedRequest Paging { get; init; } = new();
}

/// <summary>Filtering and paging criteria for the order list query.</summary>
public sealed record OrderFilter
{
    /// <summary>Restricts results to a single shopper. Administrators leave this null to see every order.</summary>
    public Guid? UserId { get; init; }

    public Domain.Enums.OrderStatus? Status { get; init; }

    public PagedRequest Paging { get; init; } = new();
}