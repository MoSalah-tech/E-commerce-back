using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

/// <summary>One requested product supplied to <see cref="Order.Create"/>.</summary>
public sealed record OrderDraftLine(Guid ProductId, string ProductName, string Sku, Money UnitPrice, int Quantity);

/// <summary>
/// A placed order. Aggregate root owning the status machine, the money
/// calculation and the shipping cost policy.
/// </summary>
public class Order : Entity, IAggregateRoot
{
    /// <summary>Standard shipping is free once the basket reaches this amount.</summary>
    public const decimal FreeShippingThreshold = 100m;

    public const decimal StandardShippingCost = 9.99m;

    public const decimal ExpressShippingCost = 24.99m;

    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public string OrderNumber { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public DateTime OrderDate { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;

    public ShippingMethod ShippingMethod { get; private set; } = ShippingMethod.Standard;

    public ShippingAddress ShippingAddress { get; private set; } = null!;

    public Money SubTotal { get; private set; } = Money.Zero();

    public Money DiscountAmount { get; private set; } = Money.Zero();

    public Money ShippingCost { get; private set; } = Money.Zero();

    public Money TotalAmount { get; private set; } = Money.Zero();

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public int ItemsCount => _items.Count;

    public int TotalQuantity => _items.Sum(x => x.Quantity);

    public bool IsPaid => PaymentStatus == PaymentStatus.Paid;

    public bool CanBeCancelled => Status is OrderStatus.Pending or OrderStatus.Confirmed;

    public static Order Create(
        Guid customerId,
        IEnumerable<OrderDraftLine> lines,
        ShippingAddress shippingAddress,
        ShippingMethod shippingMethod = ShippingMethod.Standard,
        Money? discountAmount = null,
        DateTime? orderDate = null)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("An order must belong to a customer.");

        ArgumentNullException.ThrowIfNull(shippingAddress);

        var draftLines = lines?.ToList() ?? [];

        if (draftLines.Count == 0)
            throw new DomainException("An order must contain at least one item.");

        if (draftLines.Any(x => x.Quantity <= 0))
            throw new DomainException("Every order item must have a quantity greater than zero.");

        if (discountAmount is not null && discountAmount.Amount < 0)
            throw new DomainException("Discount amount cannot be negative.");

        var now = orderDate ?? DateTime.UtcNow;

        var order = new Order
        {
            CustomerId = customerId,
            OrderNumber = OrderNumberGenerator.Generate(now),
            OrderDate = now,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            ShippingMethod = shippingMethod,
            ShippingAddress = shippingAddress
        };

        foreach (var line in draftLines)
        {
            order._items.Add(new OrderItem(
                line.ProductId,
                line.ProductName,
                line.Sku,
                line.UnitPrice,
                line.Quantity));
        }

        order.RecalculateTotals(discountAmount);
        return order;
    }

    public void Confirm()
    {
        EnsureStatus(OrderStatus.Pending, nameof(Confirm));
        Status = OrderStatus.Confirmed;
        Touch();
    }

    public void Ship()
    {
        EnsureStatus(OrderStatus.Confirmed, nameof(Ship));
        Status = OrderStatus.Shipped;
        Touch();
    }

    public void Deliver()
    {
        EnsureStatus(OrderStatus.Shipped, nameof(Deliver));
        Status = OrderStatus.Delivered;
        Touch();
    }

    /// <summary>Cancelling is only legal while the order has not left the warehouse.</summary>
    public void Cancel()
    {
        if (!CanBeCancelled)
        {
            throw new DomainException(
                $"An order in status '{Status}' cannot be cancelled. Only pending or confirmed orders can be.");
        }

        Status = OrderStatus.Cancelled;
        Touch();
    }

    public void MarkAsPaid()
    {
        if (PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded)
        {
            throw new DomainException($"An order with payment status '{PaymentStatus}' cannot be marked as paid.");
        }

        PaymentStatus = PaymentStatus.Paid;
        Touch();
    }

    public void MarkAsPaymentFailed()
    {
        PaymentStatus = PaymentStatus.Failed;
        Touch();
    }

    public void Refund()
    {
        if (!IsPaid)
            throw new DomainException("Only a paid order can be refunded.");

        PaymentStatus = PaymentStatus.Refunded;
        Status = OrderStatus.Refunded;
        Touch();
    }

    /// <summary>Recomputes subtotal, shipping and total. Called on creation and after price changes.</summary>
    public void RecalculateTotals(Money? discountAmount = null)
    {
        var currency = _items.Count > 0 ? _items[0].UnitPrice.Currency : Money.DefaultCurrency;

        SubTotal = _items.Aggregate(Money.Zero(currency), (total, item) => total + item.LineTotal);
        DiscountAmount = discountAmount ?? Money.Zero(currency);
        ShippingCost = CalculateShippingCost(SubTotal, ShippingMethod, currency);
        TotalAmount = SubTotal - DiscountAmount + ShippingCost;
        Touch();
    }

    /// <summary>
    /// Shipping policy: standard shipping is free at or above
    /// <see cref="FreeShippingThreshold"/>; express always costs a flat fee.
    /// </summary>
    public static Money CalculateShippingCost(Money subTotal, ShippingMethod method, string? currency = null)
    {
        var code = currency ?? subTotal.Currency;

        var cost = method switch
        {
            ShippingMethod.Express => ExpressShippingCost,
            _ when subTotal.Amount >= FreeShippingThreshold => 0m,
            _ => StandardShippingCost
        };

        return new Money(cost, code);
    }

    private void EnsureStatus(OrderStatus expected, string operation)
    {
        if (Status != expected)
            throw new DomainException($"Cannot {operation} an order in status '{Status}'. Expected '{expected}'.");
    }
}