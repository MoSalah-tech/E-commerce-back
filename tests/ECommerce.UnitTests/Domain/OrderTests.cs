using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.UnitTests.DomainTests;

public sealed class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTime FixedDate = new(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);

    private static ShippingAddress CreateAddress() =>
        ShippingAddress.Create(
            "Jane Doe",
            "+1 555 0100",
            "1 Infinite Loop",
            "Cupertino",
            "95014",
            "USA");

    private static OrderDraftLine Line(decimal unitPrice = 25m, int quantity = 2, string sku = "SKU-1") =>
        new(Guid.NewGuid(), "Widget", sku, Money.From(unitPrice), quantity);

    private static Order CreateOrder(params OrderDraftLine[] lines) =>
        Order.Create(
            CustomerId,
            lines.Length == 0 ? [Line()] : lines,
            CreateAddress(),
            orderDate: FixedDate);

    [Fact]
    public void Create_StartsPendingAndUnpaid()
    {
        var order = CreateOrder();

        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
        order.CustomerId.Should().Be(CustomerId);
        order.OrderDate.Should().Be(FixedDate);
        order.OrderNumber.Should().NotBeNullOrWhiteSpace();
        order.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_CalculatesTotals()
    {
        var order = CreateOrder(Line(25m, 2));

        order.SubTotal.Amount.Should().Be(50m);
        order.DiscountAmount.Amount.Should().Be(0m);
        order.ShippingCost.Amount.Should().Be(Order.StandardShippingCost);
        order.TotalAmount.Amount.Should().Be(50m + Order.StandardShippingCost);
        order.ItemsCount.Should().Be(1);
        order.TotalQuantity.Should().Be(2);
    }

    [Fact]
    public void Create_AppliesDiscountBeforeShipping()
    {
        var order = Order.Create(
            CustomerId,
            [Line(100m, 1)],
            CreateAddress(),
            ShippingMethod.Express,
            Money.From(20m),
            FixedDate);

        order.SubTotal.Amount.Should().Be(100m);
        order.DiscountAmount.Amount.Should().Be(20m);
        order.ShippingCost.Amount.Should().Be(Order.ExpressShippingCost);
        order.TotalAmount.Amount.Should().Be(100m - 20m + Order.ExpressShippingCost);
    }

    [Fact]
    public void Create_NoLines_Throws() =>
        FluentActions.Invoking(() => Order.Create(CustomerId, [], CreateAddress()))
            .Should().Throw<DomainException>().WithMessage("*at least one item*");

    [Fact]
    public void Create_NullLines_Throws() =>
        FluentActions.Invoking(() => Order.Create(CustomerId, null!, CreateAddress()))
            .Should().Throw<DomainException>();

    [Fact]
    public void Create_EmptyCustomerId_Throws() =>
        FluentActions.Invoking(() => Order.Create(Guid.Empty, [Line()], CreateAddress()))
            .Should().Throw<DomainException>().WithMessage("*must belong to a customer*");

    [Fact]
    public void Create_NullAddress_Throws() =>
        FluentActions.Invoking(() => Order.Create(CustomerId, [Line()], null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void Create_NonPositiveQuantity_Throws() =>
        FluentActions.Invoking(() => Order.Create(CustomerId, [Line(10m, 0)], CreateAddress()))
            .Should().Throw<DomainException>().WithMessage("*quantity greater than zero*");

    [Fact]
    public void Create_NegativeDiscount_Throws() =>
        FluentActions.Invoking(() => Order.Create(CustomerId, [Line()], CreateAddress(), discountAmount: Money.From(-1m)))
            .Should().Throw<DomainException>().WithMessage("*Discount amount cannot be negative*");

[Theory]
    [InlineData(ShippingMethod.Standard, 99.99, 9.99)]
    [InlineData(ShippingMethod.Standard, 100.00, 0.00)]
    [InlineData(ShippingMethod.Standard, 250.00, 0.00)]
    [InlineData(ShippingMethod.Express, 250.00, 24.99)]
    [InlineData(ShippingMethod.Express, 1.00, 24.99)]
    public void CalculateShippingCost_AppliesShippingPolicy(
        ShippingMethod method, decimal subTotal, decimal expected) =>
        Order.CalculateShippingCost(Money.From(subTotal), method).Amount.Should().Be(expected);

    [Fact]
    public void StatusMachine_FollowsHappyPath()
    {
        var order = CreateOrder();

        order.Confirm();
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.Ship();
        order.Status.Should().Be(OrderStatus.Shipped);

        order.Deliver();
        order.Status.Should().Be(OrderStatus.Delivered);
        order.CanBeCancelled.Should().BeFalse();
    }

    [Fact]
    public void Confirm_FromWrongStatus_Throws()
    {
        var order = CreateOrder();
        order.Confirm();

        FluentActions.Invoking(() => order.Confirm())
            .Should().Throw<DomainException>().WithMessage("*Cannot Confirm an order in status*");
    }

    [Fact]
    public void Ship_BeforeConfirm_Throws()
    {
        var order = CreateOrder();

        FluentActions.Invoking(() => order.Ship())
            .Should().Throw<DomainException>().WithMessage("*Cannot Ship an order in status*");
    }

    [Fact]
    public void Deliver_BeforeShip_Throws()
    {
        var order = CreateOrder();
        order.Confirm();

        FluentActions.Invoking(() => order.Deliver())
            .Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void CanBeCancelled_ReflectsStatus(OrderStatus target, bool expected)
    {
        var order = CreateOrder();

        if (target is OrderStatus.Confirmed)
            order.Confirm();
        else if (target is OrderStatus.Shipped)
        {
            order.Confirm();
            order.Ship();
        }
        else if (target is OrderStatus.Delivered)
        {
            order.Confirm();
            order.Ship();
            order.Deliver();
        }
        else if (target is OrderStatus.Cancelled)
            order.Cancel();

        order.CanBeCancelled.Should().Be(expected);
    }

    [Fact]
    public void Cancel_FromPending_Succeeds()
    {
        var order = CreateOrder();

        order.Cancel();

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromShipped_Throws()
    {
        var order = CreateOrder();
        order.Confirm();
        order.Ship();

        FluentActions.Invoking(() => order.Cancel())
            .Should().Throw<DomainException>().WithMessage("*cannot be cancelled*");
    }

    [Fact]
    public void MarkAsPaid_SetsPaymentStatus()
    {
        var order = CreateOrder();

        order.MarkAsPaid();

        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
        order.IsPaid.Should().BeTrue();
    }

    [Fact]
    public void MarkAsPaid_Twice_Throws()
    {
        var order = CreateOrder();
        order.MarkAsPaid();

        FluentActions.Invoking(() => order.MarkAsPaid())
            .Should().Throw<DomainException>().WithMessage("*cannot be marked as paid*");
    }

    [Fact]
    public void Refund_RequiresPaidOrder()
    {
        var unpaid = CreateOrder();

        FluentActions.Invoking(() => unpaid.Refund())
            .Should().Throw<DomainException>().WithMessage("*Only a paid order can be refunded*");

        var paid = CreateOrder();
        paid.MarkAsPaid();

        paid.Refund();

        paid.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        paid.Status.Should().Be(OrderStatus.Refunded);
    }

    [Fact]
    public void MarkAsPaymentFailed_SetsFailed()
    {
        var order = CreateOrder();

        order.MarkAsPaymentFailed();

        order.PaymentStatus.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void RecalculateTotals_ReflectsNewLinePrices()
    {
        var order = CreateOrder(Line(10m, 2), Line(5m, 1, "SKU-2"));

        order.RecalculateTotals();

        order.SubTotal.Amount.Should().Be(25m);
        order.TotalQuantity.Should().Be(3);
    }

    [Fact]
    public void OrderNumber_IsUniquePerOrder()
    {
        var first = CreateOrder();
        var second = CreateOrder();

        first.OrderNumber.Should().NotBe(second.OrderNumber);
    }

    [Fact]
    public void OrderNumber_ContainsTheOrderDate()
    {
        var order = CreateOrder();

        order.OrderNumber.Should().Contain(FixedDate.ToString("yyyyMMdd"));
    }
}