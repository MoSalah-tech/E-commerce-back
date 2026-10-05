using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

/// <summary>A reusable address stored on a customer profile.</summary>
public class Address : Entity
{
    private Address()
    {
    }

    public Address(
        string recipientName,
        string phoneNumber,
        string line1,
        string city,
        string postalCode,
        string country,
        AddressType type = AddressType.Shipping,
        string? line2 = null,
        string? state = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(line1);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        RecipientName = recipientName.Trim();
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        Line1 = line1.Trim();
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim();
        City = city.Trim();
        State = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        PostalCode = postalCode?.Trim() ?? string.Empty;
        Country = country?.Trim() ?? string.Empty;
        Type = type;
    }

    public string RecipientName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string Line1 { get; private set; } = string.Empty;

    public string? Line2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? State { get; private set; }

    public string PostalCode { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public AddressType Type { get; private set; }

    public bool IsDefault { get; private set; }

    public Guid CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public void UpdateDetails(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(line1);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        RecipientName = recipientName.Trim();
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        Line1 = line1.Trim();
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim();
        City = city.Trim();
        State = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        PostalCode = postalCode?.Trim() ?? string.Empty;
        Country = country?.Trim() ?? string.Empty;
        Touch();
    }

    public void ChangeType(AddressType type)
    {
        Type = type;
        Touch();
    }

    public void MarkAsDefault()
    {
        IsDefault = true;
        Touch();
    }

    public void MarkAsNotDefault()
    {
        IsDefault = false;
        Touch();
    }
}