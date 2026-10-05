using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders;

public sealed class ShippingAddressDto
{
    public string RecipientName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Line1 { get; set; } = string.Empty;

    public string? Line2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? State { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
}

public sealed class OrderItemDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public sealed class OrderDto
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public DateTime OrderDate { get; set; }

    public OrderStatus Status { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public ShippingMethod ShippingMethod { get; set; }

    public ShippingAddressDto ShippingAddress { get; set; } = new();

    public IReadOnlyList<OrderItemDto> Items { get; set; } = [];

    public int ItemsCount { get; set; }

    public int TotalQuantity { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "USD";
}

public sealed class PlaceOrderRequest
{
    public ShippingAddressDto ShippingAddress { get; set; } = new();

    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.Standard;

    /// <summary>When true the order is immediately marked as paid (used by simulated payments).</summary>
    public bool MarkAsPaid { get; set; } = true;
}

public sealed class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}