namespace ECommerce.Domain.ValueObjects;

/// <summary>
/// Immutable postal address captured as a snapshot on an order.
/// Deliberately separate from <c>Address</c> (a stored customer entity) so that
/// editing a saved address can never rewrite the history of a placed order.
/// </summary>
public sealed class ShippingAddress
{
    /// <summary>Required by the persistence layer to materialise an owned type.</summary>
    private ShippingAddress()
    {
    }

    public string RecipientName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string Line1 { get; private set; } = string.Empty;

    public string? Line2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? State { get; private set; }

    public string PostalCode { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public static ShippingAddress Create(
        string recipientName,
        string phoneNumber,
        string line1,
        string city,
        string postalCode,
        string country,
        string? line2 = null,
        string? state = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(line1);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        return new ShippingAddress
        {
            RecipientName = recipientName.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            Line1 = line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            City = city.Trim(),
            State = string.IsNullOrWhiteSpace(state) ? null : state.Trim(),
            PostalCode = postalCode.Trim(),
            Country = country.Trim()
        };
    }

    /// <summary>Single-line rendering used for logs and order summaries.</summary>
    public string ToSingleLine() =>
        $"{RecipientName}, {Line1}{(string.IsNullOrWhiteSpace(Line2) ? string.Empty : ", " + Line2)}, " +
        $"{City}{(string.IsNullOrWhiteSpace(State) ? string.Empty : ", " + State)}, {PostalCode}, {Country}";
}