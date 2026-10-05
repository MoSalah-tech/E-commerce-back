using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.UnitTests.DomainTests;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_NormalisesCurrencyToUpperCase()
    {
        var money = new Money(19.99m, "usd");

        money.Currency.Should().Be("USD");
        money.Amount.Should().Be(19.99m);
    }

    [Theory]
    [InlineData(10.005)]
    [InlineData(1.2345)]
    public void Constructor_RejectsMoreThanTwoDecimalPlaces(decimal amount)
    {
        var act = () => new Money(amount);

        act.Should().Throw<DomainException>().WithMessage("*more than two decimal places*");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Constructor_RejectsNonThreeLetterCurrency(string currency)
    {
        var act = () => new Money(5m, currency);

        act.Should().Throw<DomainException>().WithMessage("*three letter ISO code*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankCurrency_ThrowsArgumentException(string currency) =>
        FluentActions.Invoking(() => new Money(5m, currency))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Addition_WithSameCurrency_SumsAmounts()
    {
        var total = new Money(10.50m) + new Money(4.25m);

        total.Amount.Should().Be(14.75m);
        total.Currency.Should().Be("USD");
    }

    [Fact]
    public void Addition_WithDifferentCurrency_Throws()
    {
        var usd = new Money(10m, "USD");
        var eur = new Money(10m, "EUR");

        var act = () => usd + eur;

        act.Should().Throw<DomainException>().WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtraction_CanProduceNegativeAmount()
    {
        var difference = new Money(5m) - new Money(9.99m);

        difference.Amount.Should().Be(-4.99m);
        difference.IsZero.Should().BeFalse();
    }

    [Theory]
    [InlineData(10.05, 1.005, 10.10)]
    [InlineData(19.99, 3.0, 59.97)]
    [InlineData(10.00, 2.0, 20.00)]
    public void MultiplyByDecimal_RoundsToTwoDecimalPlaces(decimal amount, decimal multiplier, decimal expected) =>
        (Money.From(amount) * multiplier).Amount.Should().Be(expected);

    [Fact]
    public void ComparisonOperators_CompareAmountsWithinSameCurrency()
    {
        var cheap = new Money(10m);
        var expensive = new Money(20m);

        (cheap < expensive).Should().BeTrue();
        (expensive > cheap).Should().BeTrue();
        (cheap <= expensive).Should().BeTrue();
        (expensive >= cheap).Should().BeTrue();
    }

    [Fact]
    public void ComparisonAcrossCurrencies_Throws()
    {
        var usd = new Money(10m, "USD");
        var jpy = new Money(10m, "JPY");

        var act = () => _ = usd > jpy;

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(25.0, 10.0, 22.50)]
    [InlineData(20.0, 50.0, 10.00)]
    [InlineData(19.99, 0.0, 19.99)]
    public void ApplyPercentageDiscount_ComputesExpectedPrice(decimal amount, decimal percentage, decimal expected)
    {
        var discounted = Money.From(amount).ApplyPercentageDiscount(percentage);

        discounted.Amount.Should().Be(expected);
    }

    [Fact]
    public void ApplyPercentageDiscount_WithZeroPercentage_ReturnsSameInstance()
    {
        var money = Money.From(42m);

        money.ApplyPercentageDiscount(0m).Should().BeSameAs(money);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    public void ApplyPercentageDiscount_OutsideRange_Throws(decimal percentage)
    {
        var act = () => Money.From(10m).ApplyPercentageDiscount(percentage);

        act.Should().Throw<DomainException>().WithMessage("*between 0 and 100*");
    }

    [Fact]
    public void Zero_IsAZeroAmount()
    {
        Money.Zero().IsZero.Should().BeTrue();
        Money.From(0.01m).IsZero.Should().BeFalse();
    }

    [Fact]
    public void ImplicitConversionFromDecimal_UsesDefaultCurrency()
    {
        Money money = 33.33m;

        money.Amount.Should().Be(33.33m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void ToString_UsesInvariantTwoDecimalFormat()
    {
        Money.From(1234.5m).ToString().Should().Be("1234.50 USD");
    }
}