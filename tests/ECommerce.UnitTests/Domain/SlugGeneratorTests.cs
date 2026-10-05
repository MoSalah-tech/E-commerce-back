using ECommerce.Domain.Common;

namespace ECommerce.UnitTests.DomainTests;

public sealed class SlugGeneratorTests
{
    [Theory]
    [InlineData("Wireless Headphones", "wireless-headphones")]
    [InlineData("  USB-C   Cable  ", "usb-c-cable")]
    [InlineData("100% Cotton T-Shirt", "100-cotton-t-shirt")]
    [InlineData("Café Latte Mug", "cafe-latte-mug")]
    [InlineData("Already-Slugged", "already-slugged")]
    public void From_ProducesUrlFriendlySlug(string input, string expected) =>
        SlugGenerator.From(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void From_BlankInput_ReturnsEmptySlug(string? input) =>
        SlugGenerator.From(input!).Should().BeEmpty();

    [Fact]
    public void From_StripsLeadingAndTrailingSeparators() =>
        SlugGenerator.From("...hello world!!!").Should().Be("hello-world");
}