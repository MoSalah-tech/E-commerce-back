using ECommerce.Domain.Common;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

/// <summary>
/// A single line on a placed order. Product name, SKU and price are frozen at
/// purchase time so later catalogue edits never corrupt order history.
/// </summary>
public class OrderItem : Entity
{
    private OrderItem()
    {
    }

    internal OrderItem(
        Guid productId,
        string productName,
        string sku,
        Money unitPrice,
        int quantity)
    {
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid OrderId { get; private set; }

    public Order? Order { get; private set; }

    public Guid ProductId { get; private set; }

    public Product? Product { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public Money UnitPrice { get; private set; } = Money.Zero();

    public int Quantity { get; private set; }

    public Money LineTotal => UnitPrice * Quantity;
}