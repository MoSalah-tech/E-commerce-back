namespace ECommerce.Domain.Exceptions;

/// <summary>
/// Raised when a domain invariant would be violated. The application layer maps
/// this to a 422 Unprocessable Entity response.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, params string[] errors)
        : base(message)
    {
        Errors = errors.Length > 0 ? errors : [message];
    }

    public IReadOnlyList<string> Errors { get; } = [];
}

/// <summary>Raised when an operation requires more stock than is currently available.</summary>
public class InsufficientStockException : DomainException
{
    public InsufficientStockException(string productName, int requested, int available)
        : base(
            $"Insufficient stock for product '{productName}'. Requested: {requested}, available: {available}.")
    {
        ProductName = productName;
        Requested = requested;
        Available = available;
    }

    public string ProductName { get; }

    public int Requested { get; }

    public int Available { get; }
}