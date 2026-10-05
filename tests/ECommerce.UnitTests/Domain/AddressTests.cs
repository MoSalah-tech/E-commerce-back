using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.UnitTests.DomainTests;

public sealed class AddressTests
{
    [Fact]
    public void Constructor_TrimsAndDefaultsToShipping()
    {
        var address = new Address("  Jane  ", "  555  ", "  1 Infinite Loop  ", " Cupertino ", " 95014 ", " USA ");

        address.RecipientName.Should().Be("Jane");
        address.PhoneNumber.Should().Be("555");
        address.Line1.Should().Be("1 Infinite Loop");
        address.City.Should().Be("Cupertino");
        address.PostalCode.Should().Be("95014");
        address.Country.Should().Be("USA");
        address.Type.Should().Be(AddressType.Shipping);
        address.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Constructor_MissingOptionalPhoneBecomesEmptyString()
    {
        var address = new Address("Jane", null!, "Line1", "City", null!, null!);

        address.PhoneNumber.Should().BeEmpty();
        address.PostalCode.Should().BeEmpty();
        address.Country.Should().BeEmpty();
    }

    [Theory]
    [InlineData("recipient")]
    [InlineData("line1")]
    [InlineData("city")]
    public void Constructor_BlankRequiredField_Throws(string blank)
    {
        var act = () => blank switch
        {
            "recipient" => new Address("  ", "555", "Line1", "City", "12345", "USA"),
            "line1" => new Address("Jane", "555", "  ", "City", "12345", "USA"),
            _ => new Address("Jane", "555", "Line1", "  ", "12345", "USA")
        };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ReplacesFields()
    {
        var address = new Address("Jane", "555", "Line1", "City", "12345", "USA");

        address.UpdateDetails(" John ", " 999 ", " Line2 ", " Town ", "54321 ", " Canada ", " Apt 4 ", " QC ");

        address.RecipientName.Should().Be("John");
        address.PhoneNumber.Should().Be("999");
        address.Line1.Should().Be("Line2");
        address.Line2.Should().Be("Apt 4");
        address.City.Should().Be("Town");
        address.State.Should().Be("QC");
        address.PostalCode.Should().Be("54321");
        address.Country.Should().Be("Canada");
    }

    [Fact]
    public void ChangeType_SwitchesBillingAndShipping()
    {
        var address = new Address("Jane", "555", "Line1", "City", "12345", "USA");

        address.ChangeType(AddressType.Billing);

        address.Type.Should().Be(AddressType.Billing);
    }

    [Fact]
    public void DefaultFlag_TogglesBothWays()
    {
        var address = new Address("Jane", "555", "Line1", "City", "12345", "USA");

        address.MarkAsDefault();
        address.IsDefault.Should().BeTrue();

        address.MarkAsNotDefault();
        address.IsDefault.Should().BeFalse();
    }
}