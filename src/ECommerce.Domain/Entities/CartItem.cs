using ECommerce.Domain.Common;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

/// <summary>A single line in a customer's cart. Stores a price snapshot.</summary>
public class CartItem : Entity
{
    private CartItem()
    {
    }

    internal CartItem(Product product, int quantity, Money unitPrice)
    {
        ProductId = product.Id;
        Product = product;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid CartId { get; private set; }

    public Cart? Cart { get; private set; }

    public Guid ProductId { get; private set; }

    public Product? Product { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Price at the moment the item was added, so the cart total is stable while browsing.</summary>
    public Money UnitPrice { get; private set; } = Money.Zero();

    public Money LineTotal => UnitPrice * Quantity;

    internal void SetQuantity(int quantity) => Quantity = quantity;

    internal void RefreshPrice(Money unitPrice) => UnitPrice = unitPrice;
}