using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.ValueObjects;

/// <summary>
/// Immutable monetary amount paired with an ISO currency code.
/// Prevents cross-currency arithmetic bugs such as adding USD to EUR.
/// </summary>
public sealed record Money
{
    public const string DefaultCurrency = "USD";

    /// <summary>Required by the persistence layer to materialise an owned type.</summary>
    private Money()
    {
    }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException(
                $"Amount '{amount}' has more than two decimal places, which is not a valid monetary value.");
        }

        if (currency.Length != 3)
            throw new DomainException($"Currency '{currency}' must be a three letter ISO code.");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = DefaultCurrency;

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money From(decimal amount, string currency = DefaultCurrency) => new(amount, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "+");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "-");
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int multiplier) =>
        new(money.Amount * multiplier, money.Currency);

    public static Money operator *(Money money, decimal multiplier) =>
        new(Math.Round(money.Amount * multiplier, 2, MidpointRounding.ToEven), money.Currency);

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right, ">");
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "<");
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right, ">=");
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "<=");
        return left.Amount <= right.Amount;
    }

    public static explicit operator decimal(Money money) => money.Amount;

    public static implicit operator Money(decimal amount) => From(amount);

    /// <summary>Applies a percentage discount (0-100) and rounds half-to-even to two decimals.</summary>
    public Money ApplyPercentageDiscount(decimal percentage)
    {
        if (percentage is < 0 or > 100)
            throw new DomainException($"Discount percentage must be between 0 and 100, but was {percentage}.");

        if (percentage == 0)
            return this;

        var factor = (100m - percentage) / 100m;
        return new Money(Math.Round(Amount * factor, 2, MidpointRounding.ToEven), Currency);
    }

    public bool IsZero => Amount == 0m;

    public override string ToString() =>
        $"{Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right, string @operator)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"Cannot apply operator '{@operator}' to money in different currencies " +
                $"('{left.Currency}' and '{right.Currency}').");
        }
    }
}