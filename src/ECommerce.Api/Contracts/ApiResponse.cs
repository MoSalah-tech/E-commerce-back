using System.Text.Json.Serialization;

namespace ECommerce.Api.Contracts;

/// <summary>Uniform response envelope for every endpoint.</summary>
public sealed class ApiResponse
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ApiError>? Errors { get; init; }

    public string? TraceId { get; init; }

    public static ApiResponse Ok(object? data = null, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse Failure(
        string? message,
        IReadOnlyList<ApiError>? errors = null,
        string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}

public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Details { get; init; }

    public ApiError()
    {
    }

    public ApiError(string code, string message, IReadOnlyList<string>? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }
}