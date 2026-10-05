namespace ECommerce.Domain.Enums;

/// <summary>Lifecycle of an order. Transitions are validated inside <c>Order</c>.</summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4,
    Refunded = 5
}

/// <summary>Payment settlement state of an order.</summary>
public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3
}

/// <summary>Shipping speed selected by the customer at checkout.</summary>
public enum ShippingMethod
{
    Standard = 0,
    Express = 1
}

/// <summary>Purpose a stored customer address serves.</summary>
public enum AddressType
{
    Shipping = 0,
    Billing = 1
}