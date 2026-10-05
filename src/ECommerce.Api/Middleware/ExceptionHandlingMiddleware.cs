using System.Text.Json;
using ECommerce.Api.Contracts;
using ECommerce.Application.Common;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Exceptions;
using ValidationException = ECommerce.Application.Exceptions.ValidationException;

namespace ECommerce.Api.Middleware;

/// <summary>
/// Terminal exception handler. Converts known exceptions into the same response
/// envelope the application layer uses, so clients see one consistent error shape.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning(exception, "Response already started; cannot write the error envelope.");
            throw exception;
        }

        var (status, error) = Translate(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Request failed with {StatusCode}", status);
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var response = ApiResponse.Failure(error.Message, [error], context.TraceIdentifier);

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }

    private static (int Status, ApiError Error) Translate(Exception exception) =>
        exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                new ApiError(
                    ErrorCodes.ValidationFailed,
                    "One or more validation errors occurred.",
                    validation.Errors)),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                new ApiError(ErrorCodes.NotFound, notFound.Message)),

            ForbiddenException forbidden => (
                StatusCodes.Status403Forbidden,
                new ApiError(ErrorCodes.Forbidden, forbidden.Message)),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                new ApiError(ErrorCodes.Conflict, conflict.Message)),

            InsufficientStockException stock => (
                StatusCodes.Status409Conflict,
                new ApiError(ErrorCodes.InsufficientStock, stock.Message)),

            DomainException domain => (
                StatusCodes.Status422UnprocessableEntity,
                new ApiError(ErrorCodes.DomainRuleViolated, domain.Message, domain.Errors)),

            UnauthorizedAccessException unauthorized => (
                StatusCodes.Status401Unauthorized,
                new ApiError(ErrorCodes.Unauthorized, "Authentication is required.")),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ApiError(ErrorCodes.Unexpected, "An unexpected error occurred while processing the request."))
        };
}