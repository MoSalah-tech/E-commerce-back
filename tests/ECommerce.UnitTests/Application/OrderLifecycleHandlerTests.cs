using ECommerce.Application.Common;
using ECommerce.Application.Features.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using Moq;

namespace ECommerce.UnitTests.ApplicationTests;

public sealed class OrderLifecycleHandlerTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static readonly Guid CustomerUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private static Order CreateOrder(OrderStatus target = OrderStatus.Pending, bool markAsPaid = false)
    {
        var order = Order.Create(
            Guid.NewGuid(),
            [new OrderDraftLine(Guid.NewGuid(), "Widget", "SKU-1", Money.From(30m), 2)],
            ShippingAddress.Create("Jane", "555", "Line1", "City", "12345", "USA"));

        switch (target)
        {
            case OrderStatus.Confirmed: order.Confirm(); break;
            case OrderStatus.Shipped: order.Confirm(); order.Ship(); break;
            case OrderStatus.Delivered: order.Confirm(); order.Ship(); order.Deliver(); break;
            case OrderStatus.Cancelled: order.Cancel(); break;
        }

        if (markAsPaid)
            order.MarkAsPaid();

        return order;
    }

    private UpdateOrderStatusCommandHandler CreateUpdateHandler() =>
        new(_orders.Object, _unitOfWork.Object);

    [Fact]
    public async Task UpdateStatus_UnknownOrder_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        _orders.Setup(x => x.GetWithItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var result = await CreateUpdateHandler()
            .Handle(new UpdateOrderStatusCommand(orderId, OrderStatus.Confirmed), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Refunded)]
    public async Task UpdateStatus_AllowedTransitions_Succeed(OrderStatus target)
    {
        var order = target switch
        {
            OrderStatus.Confirmed => CreateOrder(),
            OrderStatus.Shipped => CreateOrder(OrderStatus.Confirmed),
            OrderStatus.Delivered => CreateOrder(OrderStatus.Shipped),
            OrderStatus.Cancelled => CreateOrder(),
            OrderStatus.Refunded => CreateOrder(markAsPaid: true),
            _ => CreateOrder()
        };

        _orders.Setup(x => x.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await CreateUpdateHandler()
            .Handle(new UpdateOrderStatusCommand(order.Id, target), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(target);
        _orders.Verify(x => x.Update(order), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_PendingIsNotSettableDirectly_ReturnsValidationFailure()
    {
        var order = CreateOrder();
        _orders.Setup(x => x.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await CreateUpdateHandler()
            .Handle(new UpdateOrderStatusCommand(order.Id, OrderStatus.Pending), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateStatus_IllegalTransition_ReturnsSpecificErrorCode()
    {
        var order = CreateOrder();
        _orders.Setup(x => x.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await CreateUpdateHandler()
            .Handle(new UpdateOrderStatusCommand(order.Id, OrderStatus.Shipped), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidOrderStatusTransition);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_AnonymousUser_ReturnsUnauthorized()
    {
        var handler = new CancelOrderCommandHandler(
            _orders.Object,
            _products.Object,
            _unitOfWork.Object,
            FakeCurrentUserService.Anonymous());

        var result = await handler.Handle(new CancelOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Cancel_AnotherCustomersOrder_ReturnsForbidden()
    {
        var order = CreateOrder();
        _orders.Setup(x => x.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new CancelOrderCommandHandler(
            _orders.Object,
            _products.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = OtherUserId });

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_AlreadyShippedOrder_ReturnsOrderCannotBeCancelled()
    {
        var order = CreateOrder(OrderStatus.Shipped);
        _orders.Setup(x => x.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new CancelOrderCommandHandler(
            _orders.Object,
            _products.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = CustomerUserId, Roles = [Roles.Admin] });

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.OrderCannotBeCancelled);
    }

    [Fact]
    public async Task Cancel_AdminCancelsAnyOrder_AndReturnsStockToTheCatalogue()
    {
        var product = new Product("Widget", "SKU-1", Money.From(30m), Guid.NewGuid(), stockQuantity: 1);

        var order = Order.Create(
            Guid.NewGuid(),
            [new OrderDraftLine(product.Id, product.Name, product.Sku, product.Price, 2)],
            ShippingAddress.Create("Jane", "555", "Line1", "City", "12345", "USA"));

        _orders.Setup(x => x.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _products.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var handler = new CancelOrderCommandHandler(
            _orders.Object,
            _products.Object,
            _unitOfWork.Object,
            new FakeCurrentUserService { UserId = OtherUserId, Roles = [Roles.Admin] });

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        product.StockQuantity.Should().Be(3);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}