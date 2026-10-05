using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

/// <summary>
/// A customer's shopping cart. Aggregate root that enforces quantity limits and
/// merges duplicate lines.
/// </summary>
public class Cart : Entity, IAggregateRoot
{
    private readonly List<CartItem> _items = [];

    private Cart()
    {
    }

    public Cart(Guid customerId)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("A cart must belong to a customer.");

        CustomerId = customerId;
    }

    public Guid CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public int LinesCount => _items.Count;

    public int TotalQuantity => _items.Sum(x => x.Quantity);

    public Money Total => _items.Aggregate(Money.Zero(), (total, item) => total + item.LineTotal);

    public bool IsEmpty => _items.Count == 0;

    public bool Contains(Guid productId) => _items.Any(x => x.ProductId == productId);

    public CartItem? GetItem(Guid productId) => _items.FirstOrDefault(x => x.ProductId == productId);

    /// <summary>
    /// Adds a product, or increases the quantity of the existing line. Quantity is
    /// capped at <see cref="Product.MaxQuantityPerOrder"/> rather than throwing, so the
    /// shopper gets a predictable cart.
    /// </summary>
    public CartItem AddItem(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        if (!product.IsActive)
            throw new DomainException($"Product '{product.Name}' is not available for purchase.");

        var requested = quantity;

        var existing = GetItem(product.Id);
        if (existing is not null)
        {
            requested = existing.Quantity + quantity;
            existing.SetQuantity(Math.Min(requested, Product.MaxQuantityPerOrder));
            existing.RefreshPrice(product.EffectivePrice);
            Touch();
            return existing;
        }

        var item = new CartItem(product, Math.Min(quantity, Product.MaxQuantityPerOrder), product.EffectivePrice);
        _items.Add(item);
        Touch();
        return item;
    }

    /// <summary>Sets the absolute quantity of a line. A quantity of zero removes the line.</summary>
    public CartItem UpdateItemQuantity(Guid productId, int quantity)
    {
        if (quantity < 0)
            throw new DomainException("Quantity cannot be negative.");

        var item = GetItem(productId)
            ?? throw new DomainException($"Product '{productId}' was not found in the cart.");

        if (quantity == 0)
        {
            RemoveItem(productId);
            return item;
        }

        item.SetQuantity(Math.Min(quantity, Product.MaxQuantityPerOrder));
        Touch();
        return item;
    }

    public void RemoveItem(Guid productId)
    {
        var item = GetItem(productId);

        if (item is null)
            throw new DomainException($"Product '{productId}' was not found in the cart.");

        _items.Remove(item);
        Touch();
    }

    public void Clear()
    {
        _items.Clear();
        Touch();
    }
}