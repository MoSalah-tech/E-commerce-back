namespace ECommerce.Application.Features.Reviews;

public sealed class ReviewDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string? ProductName { get; set; }

    public Guid CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public int Rating { get; set; }

    public string? Title { get; set; }

    public string? Comment { get; set; }

    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class CreateReviewRequest
{
    public Guid ProductId { get; set; }

    public int Rating { get; set; }

    public string? Title { get; set; }

    public string? Comment { get; set; }
}

public sealed class UpdateReviewRequest
{
    public int Rating { get; set; }

    public string? Title { get; set; }

    public string? Comment { get; set; }
}

public sealed class ModerateReviewRequest
{
    public bool IsApproved { get; set; }
}