namespace ECommerce.Application.Common;

/// <summary>
/// Result of an operation that either succeeds or fails with an <see cref="Error"/>.
/// Handlers return <see cref="Result"/> instead of throwing for expected failures,
/// which keeps happy-path and unhappy-path equally explicit.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot carry an error.");

        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failed result must carry an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    public static Result<TValue> Failure<TValue>(string code, string message) =>
        Failure<TValue>(new Error(code, message));
}

/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
public sealed class Result<TValue> : Result
{
    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) =>
        Value = value!;

    public TValue Value { get; }

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}