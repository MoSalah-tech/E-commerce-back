using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

/// <summary>
/// The shopper. Aggregate root that owns addresses, the cart and order history.
/// Authentication credentials live in ASP.NET Identity, not here; the two are
/// linked through <see cref="UserId"/>.
/// </summary>
public class Customer : Entity, IAggregateRoot
{
    private readonly List<Address> _addresses = [];
    private readonly List<Order> _orders = [];

    private Customer()
    {
    }

    public Customer(Guid userId, string email, string fullName, string? phoneNumber = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Customer must be linked to an authenticated user.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Customer email is required.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Customer name is required.");

        UserId = userId;
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        IsActive = true;
        Cart = new Cart(customerId: Id);
    }

    public Guid UserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();

    /// <summary>The cart is created together with the customer and therefore is never null.</summary>
    public Cart Cart { get; private set; } = null!;

    public Address? DefaultShippingAddress => _addresses.FirstOrDefault(x => x.IsDefault && x.Type == AddressType.Shipping);

    public void UpdateProfile(string fullName, string? phoneNumber, string email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Customer name is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Customer email is required.");

        FullName = fullName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        Email = email.Trim().ToLowerInvariant();
        Touch();
    }

    public void ChangeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Customer email is required.");

        Email = email.Trim().ToLowerInvariant();
        Touch();
    }

    /// <summary>
    /// Adds a stored address. The first address added for a given type is
    /// automatically promoted to default so checkout always has a target.
    /// </summary>
    public Address AddAddress(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var currentDefault = _addresses.FirstOrDefault(x => x.Type == address.Type && x.IsDefault);

        if (currentDefault is not null)
        {
            currentDefault.MarkAsNotDefault();
            address.MarkAsDefault();
        }
        else if (!_addresses.Any(x => x.Type == address.Type))
        {
            address.MarkAsDefault();
        }

        _addresses.Add(address);
        Touch();
        return address;
    }

    public void RemoveAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(x => x.Id == addressId)
            ?? throw new DomainException($"Address '{addressId}' was not found for this customer.");

        var wasDefault = address.IsDefault;
        _addresses.Remove(address);

        if (wasDefault)
        {
            var replacement = _addresses.FirstOrDefault(x => x.Type == address.Type);
            replacement?.MarkAsDefault();
        }

        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Cart.Clear();
        Touch();
    }
}