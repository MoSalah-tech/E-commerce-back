using ECommerce.Domain.Common;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.UnitTests.DomainTests;

public sealed class CartTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static Customer CreateCustomer() => new(UserId, "shopper@example.com", "Test Shopper");

    private static Product CreateProduct(string name = "Widget", string sku = "SKU-1", decimal price = 10m, int stock = 100) =>
        new(name, sku, Money.From(price), CategoryId, stockQuantity: stock);

    [Fact]
    public void Constructor_CreatesEmptyCart()
    {
        var cart = new Cart(Guid.NewGuid());

        cart.IsEmpty.Should().BeTrue();
        cart.LinesCount.Should().Be(0);
        cart.TotalQuantity.Should().Be(0);
        cart.Total.Amount.Should().Be(0m);
    }

    [Fact]
    public void Constructor_EmptyCustomerId_Throws() =>
        FluentActions.Invoking(() => new Cart(Guid.Empty))
            .Should().Throw<DomainException>().WithMessage("*must belong to a customer*");

    [Fact]
    public void AddItem_AddsNewLine()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();

        var item = cart.AddItem(product, 3);

        cart.LinesCount.Should().Be(1);
        cart.TotalQuantity.Should().Be(3);
        cart.Contains(product.Id).Should().BeTrue();
        item.Quantity.Should().Be(3);
        item.UnitPrice.Amount.Should().Be(10m);
    }

    [Fact]
    public void AddItem_SameProductTwice_MergesIntoSingleLine()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();

        cart.AddItem(product, 2);
        cart.AddItem(product, 3);

        cart.LinesCount.Should().Be(1);
        cart.GetItem(product.Id)!.Quantity.Should().Be(5);
    }

    [Fact]
    public void AddItem_CapsQuantityAtMaxPerOrder()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();

        cart.AddItem(product, 60);
        cart.AddItem(product, 60);

        cart.GetItem(product.Id)!.Quantity.Should().Be(Product.MaxQuantityPerOrder);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_NonPositiveQuantity_Throws(int quantity)
    {
        var cart = new Cart(Guid.NewGuid());

        FluentActions.Invoking(() => cart.AddItem(CreateProduct(), quantity))
            .Should().Throw<DomainException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void AddItem_InactiveProduct_Throws()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();
        product.Deactivate();

        FluentActions.Invoking(() => cart.AddItem(product, 1))
            .Should().Throw<DomainException>().WithMessage("*not available for purchase*");
    }

    [Fact]
    public void AddItem_NullProduct_Throws()
    {
        var cart = new Cart(Guid.NewGuid());

        FluentActions.Invoking(() => cart.AddItem(null!, 1)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddItem_UsesDiscountedEffectivePrice()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct(price: 100m);
        product.SetDiscount(50m);

        var item = cart.AddItem(product, 2);

        item.UnitPrice.Amount.Should().Be(50m);
        item.LineTotal.Amount.Should().Be(100m);
    }

    [Fact]
    public void UpdateItemQuantity_SetsAbsoluteQuantity()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();
        cart.AddItem(product, 5);

        cart.UpdateItemQuantity(product.Id, 2);

        cart.GetItem(product.Id)!.Quantity.Should().Be(2);
    }

    [Fact]
    public void UpdateItemQuantity_Zero_RemovesLine()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();
        cart.AddItem(product, 5);

        cart.UpdateItemQuantity(product.Id, 0);

        cart.Contains(product.Id).Should().BeFalse();
        cart.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void UpdateItemQuantity_Negative_Throws()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();
        cart.AddItem(product, 5);

        FluentActions.Invoking(() => cart.UpdateItemQuantity(product.Id, -1))
            .Should().Throw<DomainException>().WithMessage("*cannot be negative*");
    }

    [Fact]
    public void UpdateItemQuantity_UnknownProduct_Throws()
    {
        var cart = new Cart(Guid.NewGuid());

        FluentActions.Invoking(() => cart.UpdateItemQuantity(Guid.NewGuid(), 1))
            .Should().Throw<DomainException>().WithMessage("*was not found in the cart*");
    }

    [Fact]
    public void RemoveItem_DropsLine()
    {
        var cart = new Cart(Guid.NewGuid());
        var product = CreateProduct();
        cart.AddItem(product, 2);

        cart.RemoveItem(product.Id);

        cart.Contains(product.Id).Should().BeFalse();
    }

    [Fact]
    public void RemoveItem_UnknownProduct_Throws()
    {
        var cart = new Cart(Guid.NewGuid());

        FluentActions.Invoking(() => cart.RemoveItem(Guid.NewGuid()))
            .Should().Throw<DomainException>().WithMessage("*was not found in the cart*");
    }

    [Fact]
    public void Clear_EmptiesCartButKeepsItUsable()
    {
        var cart = new Cart(Guid.NewGuid());
        cart.AddItem(CreateProduct(), 3);
        cart.AddItem(CreateProduct("Other", "SKU-2"), 1);

        cart.Clear();

        cart.IsEmpty.Should().BeTrue();
        cart.Total.Amount.Should().Be(0m);
        FluentActions.Invoking(() => cart.AddItem(CreateProduct(), 1)).Should().NotThrow();
    }

    [Fact]
    public void Total_SumsLineTotals()
    {
        var cart = new Cart(Guid.NewGuid());
        cart.AddItem(CreateProduct("A", "SKU-A", 12.50m), 2);
        cart.AddItem(CreateProduct("B", "SKU-B", 7.25m), 3);

        cart.Total.Amount.Should().Be(46.75m);
        cart.TotalQuantity.Should().Be(5);
    }

    [Fact]
    public void Customer_AlwaysHasACart()
    {
        var customer = CreateCustomer();

        customer.Cart.Should().NotBeNull();
        customer.Cart.CustomerId.Should().Be(customer.Id);
        customer.Cart.IsEmpty.Should().BeTrue();
    }
}