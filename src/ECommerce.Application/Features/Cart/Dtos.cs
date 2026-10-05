namespace ECommerce.Application.Features.Cart;

public sealed class CartItemDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsInStock { get; set; }

    public int AvailableStock { get; set; }
}

public sealed class CartDto
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public IReadOnlyList<CartItemDto> Items { get; set; } = [];

    public int LinesCount { get; set; }

    public int TotalQuantity { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime UpdatedAt { get; set; }
}

public sealed class AddToCartRequest
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; } = 1;
}

public sealed class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}