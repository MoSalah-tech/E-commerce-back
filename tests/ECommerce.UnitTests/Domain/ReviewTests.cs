using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.UnitTests.DomainTests;

public sealed class ReviewTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Constructor_TrimsInputAndApprovesByDefault()
    {
        var review = new Review(ProductId, CustomerId, 4, "  Great  ", "  Works well  ");

        review.ProductId.Should().Be(ProductId);
        review.CustomerId.Should().Be(CustomerId);
        review.Rating.Should().Be(4);
        review.Title.Should().Be("Great");
        review.Comment.Should().Be("Works well");
        review.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void Constructor_BlankTitle_BecomesNull()
    {
        new Review(ProductId, CustomerId, 3, "   ").Title.Should().BeNull();
    }

    [Fact]
    public void Constructor_EmptyProductId_Throws() =>
        FluentActions.Invoking(() => new Review(Guid.Empty, CustomerId, 3))
            .Should().Throw<DomainException>().WithMessage("*must reference a product*");

    [Fact]
    public void Constructor_EmptyCustomerId_Throws() =>
        FluentActions.Invoking(() => new Review(ProductId, Guid.Empty, 3))
            .Should().Throw<DomainException>().WithMessage("*must reference a customer*");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public void Constructor_RatingOutsideRange_Throws(int rating) =>
        FluentActions.Invoking(() => new Review(ProductId, CustomerId, rating))
            .Should().Throw<DomainException>().WithMessage("*between 1 and 5*");

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_BoundaryRating_IsAccepted(int rating) =>
        new Review(ProductId, CustomerId, rating).Rating.Should().Be(rating);

    [Fact]
    public void Edit_UpdatesContent()
    {
        var review = new Review(ProductId, CustomerId, 3, "Old", "Old body");

        review.Edit(5, "  New  ", "  New body  ");

        review.Rating.Should().Be(5);
        review.Title.Should().Be("New");
        review.Comment.Should().Be("New body");
    }

    [Fact]
    public void Edit_RatingOutsideRange_Throws()
    {
        var review = new Review(ProductId, CustomerId, 3);

        FluentActions.Invoking(() => review.Edit(9, null, null))
            .Should().Throw<DomainException>().WithMessage("*between 1 and 5*");
    }

    [Fact]
    public void ApproveAndReject_ToggleModerationFlag()
    {
        var review = new Review(ProductId, CustomerId, 3);

        review.Reject();
        review.IsApproved.Should().BeFalse();

        review.Approve();
        review.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void Review_RatingBounds_AreExposed()
    {
        Review.MinRating.Should().Be(1);
        Review.MaxRating.Should().Be(5);
    }
}