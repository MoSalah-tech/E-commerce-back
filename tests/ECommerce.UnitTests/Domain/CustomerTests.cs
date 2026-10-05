using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.UnitTests.DomainTests;

public sealed class CustomerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static Customer CreateCustomer() =>
        new(UserId, "  Shopper@Example.COM ", "  Jane Doe  ", "  +1 555 0100  ");

private static Address CreateAddress(AddressType type = AddressType.Shipping, string line1 = "1 Infinite Loop") =>
        new("Jane Doe", "+1 555 0100", line1, "Cupertino", "95014", "USA", type);

    [Fact]
    public void Constructor_NormalisesInput()
    {
        var customer = CreateCustomer();

        customer.Email.Should().Be("shopper@example.com");
        customer.FullName.Should().Be("Jane Doe");
        customer.PhoneNumber.Should().Be("+1 555 0100");
        customer.IsActive.Should().BeTrue();
        customer.UserId.Should().Be(UserId);
    }

    [Fact]
    public void Constructor_OptionalPhoneNumber_IsNull()
    {
        var customer = new Customer(UserId, "a@b.com", "A B");

        customer.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void Constructor_EmptyUserId_Throws() =>
        FluentActions.Invoking(() => new Customer(Guid.Empty, "a@b.com", "A B"))
            .Should().Throw<DomainException>().WithMessage("*linked to an authenticated user*");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankEmail_Throws(string email)
    {
        FluentActions.Invoking(() => new Customer(UserId, email, "A B"))
            .Should().Throw<DomainException>().WithMessage("*email is required*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankFullName_Throws(string fullName)
    {
        FluentActions.Invoking(() => new Customer(UserId, "a@b.com", fullName))
            .Should().Throw<DomainException>().WithMessage("*name is required*");
    }

    [Fact]
    public void UpdateProfile_ReplacesFields()
    {
        var customer = CreateCustomer();

        customer.UpdateProfile("  New Name  ", "  999  ", "  NEW@Example.com  ");

        customer.FullName.Should().Be("New Name");
        customer.PhoneNumber.Should().Be("999");
        customer.Email.Should().Be("new@example.com");
        customer.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProfile_BlankName_Throws()
    {
        var customer = CreateCustomer();

        FluentActions.Invoking(() => customer.UpdateProfile("  ", null, "a@b.com"))
            .Should().Throw<DomainException>().WithMessage("*name is required*");
    }

    [Fact]
    public void ChangeEmail_LowercasesValue()
    {
        var customer = CreateCustomer();

        customer.ChangeEmail("  OTHER@Example.COM ");

        customer.Email.Should().Be("other@example.com");
    }

    [Fact]
    public void AddAddress_FirstAddressOfTypeBecomesDefault()
    {
        var customer = CreateCustomer();

        customer.AddAddress(CreateAddress());

        customer.DefaultShippingAddress.Should().NotBeNull();
    }

    [Fact]
    public void AddAddress_SecondAddressOfTypeReplacesTheDefault()
    {
        var customer = CreateCustomer();
        var first = customer.AddAddress(CreateAddress(line1: "First"));
        var second = customer.AddAddress(CreateAddress(line1: "Second"));

        first.IsDefault.Should().BeFalse();
        second.IsDefault.Should().BeTrue();
        customer.DefaultShippingAddress!.Line1.Should().Be("Second");
    }

    [Fact]
    public void AddAddress_DifferentTypeDoesNotStealTheDefault()
    {
        var customer = CreateCustomer();
        var shipping = customer.AddAddress(CreateAddress(AddressType.Shipping));
        customer.AddAddress(CreateAddress(AddressType.Billing));

        shipping.IsDefault.Should().BeTrue();
        customer.DefaultShippingAddress!.Id.Should().Be(shipping.Id);
    }

    [Fact]
    public void AddAddress_NullAddress_Throws()
    {
        var customer = CreateCustomer();

        FluentActions.Invoking(() => customer.AddAddress(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveAddress_UnknownId_Throws()
    {
        var customer = CreateCustomer();

        FluentActions.Invoking(() => customer.RemoveAddress(Guid.NewGuid()))
            .Should().Throw<DomainException>().WithMessage("*was not found for this customer*");
    }

    [Fact]
    public void RemoveAddress_PromotesReplacementWhenDefaultIsRemoved()
    {
        var customer = CreateCustomer();
        customer.AddAddress(CreateAddress(line1: "First"));
        var second = customer.AddAddress(CreateAddress(line1: "Second"));

        customer.RemoveAddress(second.Id);

        customer.Addresses.Should().HaveCount(1);
        customer.DefaultShippingAddress!.Line1.Should().Be("First");
    }

    [Fact]
    public void Deactivate_FlagsInactiveAndEmptiesCart()
    {
        var customer = CreateCustomer();
customer.Cart.AddItem(
            new Product(
                "Widget",
                "SKU-1",
                ECommerce.Domain.ValueObjects.Money.From(10m),
                Guid.NewGuid(),
                stockQuantity: 5),
            1);

        customer.Deactivate();

        customer.IsActive.Should().BeFalse();
        customer.Cart.IsEmpty.Should().BeTrue();
    }
}