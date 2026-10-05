using ECommerce.Application.Features.Cart;
using ECommerce.Application.Features.Categories;
using ECommerce.Application.Features.Orders;
using ECommerce.Application.Features.Products;
using ECommerce.Application.Features.Reviews;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Application.Mapping;

/// <summary>
/// Explicit, null-safe entity to DTO projections.
/// Hand written on purpose: the mappings are part of the API contract, they must be
/// reviewable at a glance, and they cost nothing at runtime.
/// </summary>
public static class MappingExtensions
{
    public static CategoryDto ToDto(this Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        Description = category.Description,
        ImageUrl = category.ImageUrl,
        DisplayOrder = category.DisplayOrder,
        IsActive = category.IsActive,
        ProductCount = 0,
        CreatedAt = category.CreatedAt
    };

    public static ProductDto ToDto(this Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Sku = product.Sku,
        Price = product.Price.Amount,
        EffectivePrice = product.EffectivePrice.Amount,
        CompareAtPrice = product.CompareAtPrice?.Amount,
        DiscountPercentage = product.DiscountPercentage,
        Currency = product.Price.Currency,
        IsOnSale = product.IsOnSale,
        StockQuantity = product.StockQuantity,
        IsInStock = product.IsInStock,
        IsLowOnStock = product.IsLowOnStock,
        IsActive = product.IsActive,
        IsFeatured = product.IsFeatured,
        ImageUrl = product.ImageUrl,
        AverageRating = product.AverageRating,
        ReviewCount = product.ReviewCount,
        CategoryId = product.CategoryId,
        CategoryName = product.Category?.Name,
        CreatedAt = product.CreatedAt
    };

    public static CartDto ToDto(this Cart cart)
    {
        var items = cart.Items.Select(x => x.ToDto()).ToList();

        return new CartDto
        {
            Id = cart.Id,
            CustomerId = cart.CustomerId,
            Items = items,
            LinesCount = cart.LinesCount,
            TotalQuantity = cart.TotalQuantity,
            Total = cart.Total.Amount,
            Currency = cart.Items.FirstOrDefault()?.UnitPrice.Currency ?? Money.DefaultCurrency,
            UpdatedAt = cart.UpdatedAt ?? cart.CreatedAt
        };
    }

    public static CartItemDto ToDto(this CartItem item) => new()
    {
        ProductId = item.ProductId,
        ProductName = item.Product?.Name ?? string.Empty,
        ImageUrl = item.Product?.ImageUrl,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice.Amount,
        LineTotal = item.LineTotal.Amount,
        IsInStock = item.Product?.IsInStock ?? false,
        AvailableStock = item.Product?.StockQuantity ?? 0
    };

    public static OrderDto ToDto(this Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        CustomerId = order.CustomerId,
        CustomerName = order.Customer?.FullName,
        OrderDate = order.OrderDate,
        Status = order.Status,
        PaymentStatus = order.PaymentStatus,
        ShippingMethod = order.ShippingMethod,
        ShippingAddress = order.ShippingAddress?.ToDto() ?? new ShippingAddressDto(),
        Items = order.Items.Select(x => x.ToDto()).ToList(),
        ItemsCount = order.ItemsCount,
        TotalQuantity = order.TotalQuantity,
        SubTotal = order.SubTotal.Amount,
        DiscountAmount = order.DiscountAmount.Amount,
        ShippingCost = order.ShippingCost.Amount,
        TotalAmount = order.TotalAmount.Amount,
        Currency = order.SubTotal.Currency
    };

    public static OrderItemDto ToDto(this OrderItem item) => new()
    {
        ProductId = item.ProductId,
        ProductName = item.ProductName,
        Sku = item.Sku,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice.Amount,
        LineTotal = item.LineTotal.Amount
    };

    public static ShippingAddressDto ToDto(this ShippingAddress address) => new()
    {
        RecipientName = address.RecipientName,
        PhoneNumber = address.PhoneNumber,
        Line1 = address.Line1,
        Line2 = address.Line2,
        City = address.City,
        State = address.State,
        PostalCode = address.PostalCode,
        Country = address.Country
    };

    public static ReviewDto ToDto(this Review review) => new()
    {
        Id = review.Id,
        ProductId = review.ProductId,
        ProductName = review.Product?.Name,
        CustomerId = review.CustomerId,
        CustomerName = review.Customer?.FullName,
        Rating = review.Rating,
        Title = review.Title,
        Comment = review.Comment,
        IsApproved = review.IsApproved,
        CreatedAt = review.CreatedAt
    };
}