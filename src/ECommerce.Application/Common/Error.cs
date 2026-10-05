namespace ECommerce.Application.Common;

/// <summary>A single, machine readable failure.</summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Unexpected)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string message) =>
        new(ErrorCodes.ValidationFailed, message, ErrorType.Validation);

    public static Error NotFound(string message) =>
        new(ErrorCodes.NotFound, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string message) =>
        new(ErrorCodes.Unauthorized, message, ErrorType.Unauthorized);

    public static Error Unauthorized(string code, string message) =>
        new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string message) =>
        new(ErrorCodes.Forbidden, message, ErrorType.Forbidden);

    public static Error Failure(string message) =>
        new(ErrorCodes.Unexpected, message, ErrorType.Unexpected);

    /// <summary>Wraps plain validation messages under the standard validation code.</summary>
    public static IReadOnlyList<Error> ValidationErrors(IEnumerable<string> messages) =>
        messages.Select(Validation).ToList();
}