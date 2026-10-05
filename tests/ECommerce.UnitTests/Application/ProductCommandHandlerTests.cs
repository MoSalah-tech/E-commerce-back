using ECommerce.Application.Common;
using ECommerce.Application.Features.Products;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Moq;

namespace ECommerce.UnitTests.ApplicationTests;

public sealed class ProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static readonly Guid CategoryId = Guid.NewGuid();

    private static Product CreateProduct(string sku = "SKU-1", decimal price = 20m, int stock = 10) =>
        new("Widget", sku, Money.From(price), CategoryId, stockQuantity: stock);

    private CreateProductCommandHandler CreateCreateHandler() =>
        new(_products.Object, _categories.Object, _unitOfWork.Object);

    [Fact]
    public async Task Create_UnknownCategory_ReturnsNotFound()
    {
        _categories.Setup(x => x.ExistsAsync(CategoryId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateCreateHandler().Handle(
            new CreateProductCommand("Widget", null, "SKU-1", 20m, CategoryId),
            CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
        _products.Verify(x => x.Add(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Create_DuplicateSku_ReturnsConflict()
    {
        _categories.Setup(x => x.ExistsAsync(CategoryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _products.Setup(x => x.SkuExistsAsync("SKU-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateCreateHandler().Handle(
            new CreateProductCommand("Widget", null, "SKU-1", 20m, CategoryId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.DuplicateSku);
    }

    [Fact]
    public async Task Create_HappyPath_AddsProductAndReturnsItsId()
    {
        _categories.Setup(x => x.ExistsAsync(CategoryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _products.Setup(x => x.SkuExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Product? captured = null;
        _products.Setup(x => x.Add(It.IsAny<Product>())).Callback<Product>(p => captured = p);

        var result = await CreateCreateHandler().Handle(
            new CreateProductCommand(
                "  Wireless Mouse  ",
                "  Description  ",
                "  mouse-1  ",
                49.99m,
                CategoryId,
                StockQuantity: 12,
                LowStockThreshold: 3,
                IsFeatured: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Name.Should().Be("Wireless Mouse");
        captured.Sku.Should().Be("MOUSE-1");
        captured.StockQuantity.Should().Be(12);
        captured.LowStockThreshold.Should().Be(3);
        captured.IsFeatured.Should().BeTrue();
        captured.Price.Amount.Should().Be(49.99m);
        result.Value.Should().Be(captured.Id);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_CompareAtPriceBelowSellingPrice_ReturnsValidationFailure()
    {
        _categories.Setup(x => x.ExistsAsync(CategoryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _products.Setup(x => x.SkuExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateCreateHandler().Handle(
            new CreateProductCommand("Widget", null, "SKU-1", 100m, CategoryId, CompareAtPrice: 50m),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("compare-at price");
    }

    [Fact]
    public async Task Update_UnknownProduct_ReturnsNotFound()
    {
        var productId = Guid.NewGuid();
        _products.Setup(x => x.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var handler = new UpdateProductCommandHandler(_products.Object, _categories.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new UpdateProductCommand(productId, "Widget", null, 20m, CategoryId),
            CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_HappyPath_MutatesAggregate()
    {
        var product = CreateProduct();
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _categories.Setup(x => x.ExistsAsync(CategoryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new UpdateProductCommandHandler(_products.Object, _categories.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new UpdateProductCommand(product.Id, "  Renamed  ", "  New copy  ", 30m, CategoryId, ImageUrl: "u.png", IsFeatured: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("Renamed");
        product.Description.Should().Be("New copy");
        product.Price.Amount.Should().Be(30m);
        product.IsFeatured.Should().BeTrue();
        _products.Verify(x => x.Update(product), Times.Once);
    }

    [Fact]
    public async Task Delete_SoftDeletesTheAggregate()
    {
        var product = CreateProduct();
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new DeleteProductCommandHandler(_products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _products.Verify(x => x.Remove(product), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustStock_ZeroDelta_ReturnsValidationFailure()
    {
        var product = CreateProduct(stock: 10);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new AdjustStockCommandHandler(_products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new AdjustStockCommand(product.Id, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdjustStock_PositiveDelta_Restocks()
    {
        var product = CreateProduct(stock: 10);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new AdjustStockCommandHandler(_products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new AdjustStockCommand(product.Id, 15, "Restock"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(25);
    }

    [Fact]
    public async Task AdjustStock_NegativeDelta_DeductsStock()
    {
        var product = CreateProduct(stock: 10);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new AdjustStockCommandHandler(_products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new AdjustStockCommand(product.Id, -3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(7);
    }

    [Fact]
    public async Task AdjustStock_BeyondAvailability_ReturnsDomainFailure()
    {
        var product = CreateProduct(stock: 2);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new AdjustStockCommandHandler(_products.Object, _unitOfWork.Object);

        var act = async () => await handler.Handle(new AdjustStockCommand(product.Id, -5), CancellationToken.None);

        await act.Should().ThrowAsync<ECommerce.Domain.Exceptions.InsufficientStockException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SetDiscount_OutsideRange_ReturnsValidationFailure(decimal percentage)
    {
        var product = CreateProduct();
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new SetDiscountCommandHandler(_products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new SetDiscountCommand(product.Id, percentage), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetDiscount_HappyPath_AppliesDiscount()
    {
        var product = CreateProduct(price: 80m);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var handler = new SetDiscountCommandHandler(_products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new SetDiscountCommand(product.Id, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.DiscountPercentage.Should().Be(10m);
        product.EffectivePrice.Amount.Should().Be(72m);
    }
}