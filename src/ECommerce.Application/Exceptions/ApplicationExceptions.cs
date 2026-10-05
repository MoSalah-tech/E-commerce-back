using ECommerce.Domain.Common;

namespace ECommerce.Application.Exceptions;

/// <summary>One or more validation messages; maps to HTTP 400.</summary>
public class ValidationException : Exception
{
    public ValidationException(params string[] errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors.Length > 0 ? errors : [Message];
    }

    public ValidationException(IEnumerable<string> errors)
        : this([.. errors])
    {
    }

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>A required entity does not exist; maps to HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
    }
}

/// <summary>The request conflicts with the current state; maps to HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

/// <summary>Authenticated but not allowed; maps to HTTP 403.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}