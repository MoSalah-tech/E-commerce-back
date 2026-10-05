using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

/// <summary>A customer rating left on a product. Aggregate root in its own right.</summary>
public class Review : Entity, IAggregateRoot
{
    public const int MinRating = 1;

    public const int MaxRating = 5;

    private Review()
    {
    }

    public Review(Guid productId, Guid customerId, int rating, string? title = null, string? comment = null)
    {
        if (productId == Guid.Empty)
            throw new DomainException("A review must reference a product.");

        if (customerId == Guid.Empty)
            throw new DomainException("A review must reference a customer.");

        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainException(
                $"Rating must be between {MinRating} and {MaxRating}, but was {rating}.");
        }

        ProductId = productId;
        CustomerId = customerId;
        Rating = rating;
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Comment = comment?.Trim();
        IsApproved = true;
    }

    public Guid ProductId { get; private set; }

    public Product? Product { get; private set; }

    public Guid CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public int Rating { get; private set; }

    public string? Title { get; private set; }

    public string? Comment { get; private set; }

    /// <summary>Moderation flag; only approved reviews feed the product rating.</summary>
    public bool IsApproved { get; private set; }

    public void Edit(int rating, string? title, string? comment)
    {
        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainException(
                $"Rating must be between {MinRating} and {MaxRating}, but was {rating}.");
        }

        Rating = rating;
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Comment = comment?.Trim();
        Touch();
    }

    public void Approve()
    {
        IsApproved = true;
        Touch();
    }

    public void Reject()
    {
        IsApproved = false;
        Touch();
    }
}