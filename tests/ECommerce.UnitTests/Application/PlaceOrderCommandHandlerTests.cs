using ECommerce.Application.Common;
using ECommerce.Application.Features.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using Moq;

namespace ECommerce.UnitTests.ApplicationTests;

public sealed class PlaceOrderCommandHandlerTests
{
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeEmailSender _email = new();

    private readonly Guid UserId = Guid.NewGuid();
    private readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ShippingAddressDto Address = new()
    {
        RecipientName = "Jane Doe",
        PhoneNumber = "+1 555 0100",
        Line1 = "1 Infinite Loop",
        City = "Cupertino",
        PostalCode = "95014",
        Country = "USA"
    };

    private Customer CreateCustomer(bool active = true)
    {
        var customer = new Customer(UserId, "shopper@example.com", "Jane Doe");
        if (!active)
            customer.Deactivate();

        _customers.Setup(x => x.GetWithCartAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        return customer;
    }

    private static Product CreateProduct(decimal price = 25m, int stock = 10, string sku = "SKU-1") =>
        new("Widget", sku, Money.From(price), Guid.NewGuid(), stockQuantity: stock);

    private void SetupCatalogue(params Product[] products) =>
        _products.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

    private PlaceOrderCommandHandler CreateHandler() =>
        new(
            _customers.Object,
            _products.Object,
            _orders.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = UserId },
            new FakeDateTimeProvider(Now),
            _email);

    [Fact]
    public async Task Handle_AnonymousUser_ReturnsUnauthorized()
    {
        var handler = new PlaceOrderCommandHandler(
            _customers.Object,
            _products.Object,
            _orders.Object,
            _unitOfWork.Object,
            FakeCurrentUserService.Anonymous(),
            new FakeDateTimeProvider(Now),
            _email);

        var result = await handler.Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_NoCustomerProfile_ReturnsNotFound()
    {
        _customers.Setup(x => x.GetWithCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var result = await CreateHandler().Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DeactivatedAccount_ReturnsForbidden()
    {
        CreateCustomer(active: false);

        var result = await CreateHandler().Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_EmptyCart_ReturnsConflict()
    {
        CreateCustomer();

        var result = await CreateHandler().Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.EmptyCart);
        _orders.Verify(x => x.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProductMissingFromCatalogue_ReturnsNotFound()
    {
        var customer = CreateCustomer();
        var product = CreateProduct();
        customer.Cart.AddItem(product, 1);
        SetupCatalogue();

        var result = await CreateHandler().Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Contain("no longer available");
    }

    [Fact]
    public async Task Handle_ProductDeactivated_ReturnsConflict()
    {
        var customer = CreateCustomer();
        var product = CreateProduct();
        customer.Cart.AddItem(product, 1);
        product.Deactivate();
        SetupCatalogue(product);

        var result = await CreateHandler().Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("no longer on sale");
    }

    [Fact]
    public async Task Handle_NotEnoughStock_ReturnsConflictWithStockCode()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 2);
        customer.Cart.AddItem(product, 5);
        SetupCatalogue(product);

        var result = await CreateHandler().Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InsufficientStock);
        result.Error.Message.Should().Contain("2 unit(s)");
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesOrderReservesStockAndClearsCart()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(price: 25m, stock: 10);
        customer.Cart.AddItem(product, 2);
        SetupCatalogue(product);

        var result = await CreateHandler()
            .Handle(new PlaceOrderCommand(Address, ShippingMethod.Standard, MarkAsPaid: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderNumber.Should().StartWith("ORD-");
        result.Value.SubTotal.Should().Be(50m);
        result.Value.PaymentStatus.Should().Be(PaymentStatus.Paid);
        result.Value.Status.Should().Be(OrderStatus.Pending);
        result.Value.Items.Should().ContainSingle();
        result.Value.ShippingAddress.City.Should().Be("Cupertino");
        result.Value.OrderDate.Should().Be(Now);

        product.StockQuantity.Should().Be(8);
        customer.Cart.IsEmpty.Should().BeTrue();

        _orders.Verify(x => x.Add(It.IsAny<Order>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _email.OrderConfirmations.Should().ContainSingle()
            .Which.OrderNumber.Should().Be(result.Value.OrderNumber);
    }

    [Fact]
    public async Task Handle_UsesLiveCataloguePriceRatherThanTheCartSnapshot()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(price: 25m, stock: 10);
        customer.Cart.AddItem(product, 2);

        // The shopper added the item before the discount was applied.
        product.SetDiscount(50m);
        SetupCatalogue(product);

        var result = await CreateHandler().Handle(new PlaceOrderCommand(Address), CancellationToken.None);

        result.Value.SubTotal.Should().Be(25m);
        result.Value.Items[0].UnitPrice.Should().Be(12.50m);
    }

    [Fact]
    public async Task Handle_ExpressShippingAboveFreeThreshold_StillPaysExpressFee()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(price: 100m, stock: 10);
        customer.Cart.AddItem(product, 2);
        SetupCatalogue(product);

        var result = await CreateHandler()
            .Handle(new PlaceOrderCommand(Address, ShippingMethod.Express), CancellationToken.None);

        result.Value.ShippingCost.Should().Be(Order.ExpressShippingCost);
        result.Value.TotalAmount.Should().Be(200m + Order.ExpressShippingCost);
    }

    [Fact]
    public async Task Handle_MarkAsPaidFalse_LeavesPaymentPending()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 10);
        customer.Cart.AddItem(product, 1);
        SetupCatalogue(product);

        var result = await CreateHandler()
            .Handle(new PlaceOrderCommand(Address, ShippingMethod.Standard, MarkAsPaid: false), CancellationToken.None);

        result.Value.PaymentStatus.Should().Be(PaymentStatus.Pending);
    }
}