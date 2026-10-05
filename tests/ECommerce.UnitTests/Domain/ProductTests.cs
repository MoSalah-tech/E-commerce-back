using ECommerce.Domain.Common;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.UnitTests.DomainTests;

public sealed class ProductTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static Product CreateProduct(
        string name = "Wireless Headphones",
        string sku = "WH-100",
        decimal price = 99.99m,
        int stock = 25,
        string? description = null,
        string? imageUrl = null) =>
        new(name, sku, Money.From(price), CategoryId, description, imageUrl, stock);

    [Fact]
    public void Constructor_NormalisesNameAndSku()
    {
        var product = CreateProduct("  Laptop Stand  ", "  abc-123  ");

        product.Name.Should().Be("Laptop Stand");
        product.Sku.Should().Be("ABC-123");
        product.IsActive.Should().BeTrue();
        product.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankName_Throws(string name)
    {
        var act = () => CreateProduct(name: name);

        act.Should().Throw<DomainException>().WithMessage("*Product name is required*");
    }

    [Fact]
    public void Constructor_BlankSku_Throws()
    {
        var act = () => new Product("Valid", "  ", Money.From(1m), CategoryId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyCategory_Throws() =>
        FluentActions.Invoking(() => new Product("Valid", "SKU", Money.From(1m), Guid.Empty))
            .Should().Throw<DomainException>().WithMessage("*must belong to a category*");

    [Fact]
    public void Constructor_NegativeStock_Throws()
    {
        var act = () => CreateProduct(stock: -1);

        act.Should().Throw<DomainException>().WithMessage("*Stock quantity cannot be negative*");
    }

    [Fact]
    public void Constructor_NegativePrice_Throws() =>
        FluentActions.Invoking(() => CreateProduct(price: -0.01m))
            .Should().Throw<DomainException>().WithMessage("*price cannot be negative*");

    [Fact]
    public void Stock_FlagsReflectInventoryState()
    {
        var inStock = CreateProduct(stock: 25);
        var low = CreateProduct(stock: 3);
        var none = CreateProduct(stock: 0);

        inStock.IsInStock.Should().BeTrue();
        inStock.IsLowOnStock.Should().BeFalse();

        low.IsInStock.Should().BeTrue();
        low.IsLowOnStock.Should().BeTrue();

        none.IsInStock.Should().BeFalse();
        none.IsLowOnStock.Should().BeFalse();
    }

    [Fact]
    public void HasSufficientStock_ChecksRequestedQuantity()
    {
        var product = CreateProduct(stock: 10);

        product.HasSufficientStock(10).Should().BeTrue();
        product.HasSufficientStock(11).Should().BeFalse();
        product.HasSufficientStock(0).Should().BeFalse();
        product.HasSufficientStock(-1).Should().BeFalse();
    }

    [Fact]
    public void Restock_IncreasesQuantity()
    {
        var product = CreateProduct(stock: 10);

        product.Restock(15);

        product.StockQuantity.Should().Be(25);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Restock_NonPositiveQuantity_Throws(int quantity)
    {
        var product = CreateProduct(stock: 10);

        FluentActions.Invoking(() => product.Restock(quantity))
            .Should().Throw<DomainException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void ReserveStock_DecreasesQuantity()
    {
        var product = CreateProduct(stock: 10);

        product.ReserveStock(4);

        product.StockQuantity.Should().Be(6);
    }

    [Fact]
    public void ReserveStock_ExceedingAvailability_ThrowsInsufficientStock()
    {
        var product = CreateProduct(stock: 3);

        var act = () => product.ReserveStock(5);

        act.Should().Throw<InsufficientStockException>();
    }

    [Fact]
    public void ReleaseStock_ReturnsQuantityToInventory()
    {
        var product = CreateProduct(stock: 4);

        product.ReleaseStock(6);

        product.StockQuantity.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReleaseStock_NonPositiveQuantity_Throws(int quantity)
    {
        var product = CreateProduct(stock: 10);

        FluentActions.Invoking(() => product.ReleaseStock(quantity))
            .Should().Throw<DomainException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void SetDiscount_UpdatesEffectivePrice()
    {
        var product = CreateProduct(price: 200m);

        product.SetDiscount(25m);

        product.DiscountPercentage.Should().Be(25m);
        product.IsOnSale.Should().BeTrue();
        product.EffectivePrice.Amount.Should().Be(150m);
    }

[Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void SetDiscount_OutsideRange_Throws(decimal percentage)
    {
        var product = CreateProduct();

        FluentActions.Invoking(() => product.SetDiscount(percentage))
            .Should().Throw<DomainException>().WithMessage("*between 0 and 100*");
    }

    [Fact]
    public void IsOnSale_WithoutDiscount_IsTrueOnlyWhenACompareAtPriceIsSet()
    {
        CreateProduct(price: 99.99m).IsOnSale.Should().BeFalse();

        var markedDown = CreateProduct(price: 99.99m);
        markedDown.SetCompareAtPrice(Money.From(129.99m));

        markedDown.IsOnSale.Should().BeTrue();
    }

    [Fact]
    public void SetCompareAtPrice_BelowTheSellingPrice_Throws()
    {
        var product = CreateProduct(price: 99.99m);

        FluentActions.Invoking(() => product.SetCompareAtPrice(Money.From(49.99m)))
            .Should().Throw<DomainException>().WithMessage("*greater than or equal to the selling price*");
    }


    [Fact]
    public void SetDiscount_ToHundredPercent_ProducesFreeProduct()
    {
        var product = CreateProduct(price: 50m);

        product.SetDiscount(100m);

        product.EffectivePrice.Amount.Should().Be(0m);
    }

    [Fact]
    public void SetCompareAtPrice_BelowSellingPrice_Throws()
    {
        var product = CreateProduct(price: 100m);

        FluentActions.Invoking(() => product.SetCompareAtPrice(Money.From(80m)))
            .Should().Throw<DomainException>().WithMessage("*greater than or equal to the selling price*");
    }

    [Fact]
    public void SetCompareAtPrice_ZeroAmount_IsTreatedAsAbsent()
    {
        var product = CreateProduct(price: 100m);

        product.SetCompareAtPrice(Money.Zero());

        product.CompareAtPrice.Should().BeNull();
    }

    [Fact]
    public void SetCompareAtPrice_AboveSellingPrice_IsStored()
    {
        var product = CreateProduct(price: 100m);

        product.SetCompareAtPrice(Money.From(150m));

        product.CompareAtPrice!.Amount.Should().Be(150m);
    }

    [Fact]
    public void SetLowStockThreshold_Negative_Throws()
    {
        var product = CreateProduct();

        FluentActions.Invoking(() => product.SetLowStockThreshold(-1))
            .Should().Throw<DomainException>().WithMessage("*threshold cannot be negative*");
    }

    [Fact]
    public void UpdateDetails_ReplacesMutableFields()
    {
        var product = CreateProduct();

        product.UpdateDetails(
            "  Renamed  ",
            "  New description  ",
            Money.From(129.99m),
            "  https://cdn/img.png  ",
            Guid.NewGuid(),
            isFeatured: true);

        product.Name.Should().Be("Renamed");
        product.Description.Should().Be("New description");
        product.Price.Amount.Should().Be(129.99m);
        product.ImageUrl.Should().Be("https://cdn/img.png");
        product.IsFeatured.Should().BeTrue();
    }

    [Fact]
    public void ActivateAndDeactivate_ToggleAvailability()
    {
        var product = CreateProduct();

        product.Deactivate();
        product.IsActive.Should().BeFalse();

        product.Activate();
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RecalculateRating_StoresRoundedAverage()
    {
        var product = CreateProduct();

        product.RecalculateRating(4.567m, 13);

        product.AverageRating.Should().Be(4.57m);
        product.ReviewCount.Should().Be(13);
    }

    [Fact]
    public void RecalculateRating_AverageOutsideOneToFive_Throws()
    {
        var product = CreateProduct();

        FluentActions.Invoking(() => product.RecalculateRating(5.4m, 3))
            .Should().Throw<DomainException>().WithMessage("*between 0 and 5*");
    }

    [Fact]
    public void RecalculateRating_NegativeCount_Throws()
    {
        var product = CreateProduct();

        FluentActions.Invoking(() => product.RecalculateRating(0m, -1))
            .Should().Throw<DomainException>().WithMessage("*Review count cannot be negative*");
    }

    [Fact]
    public void ChangeSku_NormalisesValue()
    {
        var product = CreateProduct();

        product.ChangeSku("  new-sku 42 ");

        product.Sku.Should().Be("NEW-SKU 42");
    }

    [Fact]
    public void MaxQuantityPerOrder_IsAHundred()
    {
        Product.MaxQuantityPerOrder.Should().Be(100);
    }

    [Fact]
    public void SoftDelete_MarksEntityAsDeleted()
    {
        var product = CreateProduct();

        product.MarkAsDeleted();

        product.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Entity_ExposesIAggregateRoot()
    {
        CreateProduct().Should().BeAssignableTo<IAggregateRoot>();
    }
}