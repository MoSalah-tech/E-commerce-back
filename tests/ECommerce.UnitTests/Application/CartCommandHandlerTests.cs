using ECommerce.Application.Common;
using ECommerce.Application.Features.Cart;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Moq;

namespace ECommerce.UnitTests.ApplicationTests;

public sealed class CartCommandHandlerTests
{
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static readonly Guid UserId = Guid.NewGuid();

    private Customer CreateCustomerWithCart()
    {
        var customer = new Customer(UserId, "shopper@example.com", "Jane Doe");
        _customers.Setup(x => x.GetWithCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        return customer;
    }

    private static Product CreateProduct(string sku = "SKU-1", decimal price = 10m, int stock = 100) =>
        new("Widget", sku, Money.From(price), Guid.NewGuid(), stockQuantity: stock);

    [Fact]
    public async Task AddToCart_AnonymousUser_ReturnsUnauthorized()
    {
        var handler = new AddToCartCommandHandler(
            _customers.Object,
            _products.Object,
            _unitOfWork.Object,
            FakeCurrentUserService.Anonymous());

        var result = await handler.Handle(new AddToCartCommand(Guid.NewGuid(), 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddToCart_UnknownProduct_ReturnsNotFound()
    {
        var productId = Guid.NewGuid();
        _products.Setup(x => x.GetByIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        var handler = new AddToCartCommandHandler(
            _customers.Object,
            _products.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new AddToCartCommand(productId, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task AddToCart_NoCustomerProfile_ReturnsNotFound()
    {
        var product = CreateProduct();
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _customers.Setup(x => x.GetWithCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var handler = new AddToCartCommandHandler(
            _customers.Object,
            _products.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new AddToCartCommand(product.Id, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task AddToCart_DomainRuleViolation_ReturnsValidationFailure()
    {
        var product = CreateProduct(stock: 1);
        product.Deactivate();
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        CreateCustomerWithCart();

        var handler = new AddToCartCommandHandler(
            _customers.Object,
            _products.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new AddToCartCommand(product.Id, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddToCart_HappyPath_PersistsAndReturnsCart()
    {
        var product = CreateProduct(price: 12.50m);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var customer = CreateCustomerWithCart();

        var handler = new AddToCartCommandHandler(
            _customers.Object,
            _products.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new AddToCartCommand(product.Id, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Quantity.Should().Be(2);
        result.Value.Total.Should().Be(25.00m);
        result.Value.LinesCount.Should().Be(1);
        customer.Cart.LinesCount.Should().Be(1);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCartItem_UnknownLine_ReturnsNotFound()
    {
        var customer = CreateCustomerWithCart();

        var handler = new UpdateCartItemCommandHandler(
            _customers.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new UpdateCartItemCommand(Guid.NewGuid(), 3), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItem_SetsQuantityAndSaves()
    {
        var product = CreateProduct();
        var customer = CreateCustomerWithCart();
        customer.Cart.AddItem(product, 5);

        var handler = new UpdateCartItemCommandHandler(
            _customers.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new UpdateCartItemCommand(product.Id, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.Cart.GetItem(product.Id)!.Quantity.Should().Be(2);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCartItem_AnonymousUser_ReturnsUnauthorized()
    {
        var handler = new UpdateCartItemCommandHandler(
            _customers.Object,
            _unitOfWork.Object,
            FakeCurrentUserService.Anonymous());

        var result = await handler.Handle(new UpdateCartItemCommand(Guid.NewGuid(), 1), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task RemoveFromCart_RemovesLineAndSaves()
    {
        var product = CreateProduct();
        var customer = CreateCustomerWithCart();
        customer.Cart.AddItem(product, 2);

        var handler = new RemoveFromCartCommandHandler(
            _customers.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new RemoveFromCartCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.Cart.Contains(product.Id).Should().BeFalse();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveFromCart_UnknownLine_ReturnsNotFound()
    {
        CreateCustomerWithCart();

        var handler = new RemoveFromCartCommandHandler(
            _customers.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new RemoveFromCartCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ClearCart_EmptiesCartAndSaves()
    {
        var product = CreateProduct();
        var customer = CreateCustomerWithCart();
        customer.Cart.AddItem(product, 2);

        var handler = new ClearCartCommandHandler(
            _customers.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new ClearCartCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.Cart.IsEmpty.Should().BeTrue();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearCart_AnonymousUser_ReturnsUnauthorized()
    {
        var handler = new ClearCartCommandHandler(
            _customers.Object,
            _unitOfWork.Object,
            FakeCurrentUserService.Anonymous());

        var result = await handler.Handle(new ClearCartCommand(), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }
}