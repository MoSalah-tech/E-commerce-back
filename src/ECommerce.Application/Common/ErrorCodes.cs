namespace ECommerce.Application.Common;

/// <summary>Classification of a failure, used by the API layer to pick an HTTP status code.</summary>
public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Unexpected = 6
}

/// <summary>Machine readable, stable error codes returned to clients.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string DuplicateEmail = "DUPLICATE_EMAIL";
    public const string DuplicateSku = "DUPLICATE_SKU";
    public const string DuplicateCategoryName = "DUPLICATE_CATEGORY_NAME";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string EmptyCart = "EMPTY_CART";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string InvalidOrderStatusTransition = "INVALID_ORDER_STATUS_TRANSITION";
    public const string OrderCannotBeCancelled = "ORDER_CANNOT_BE_CANCELLED";
    public const string ReviewAlreadyExists = "REVIEW_ALREADY_EXISTS";
    public const string DomainRuleViolated = "DOMAIN_RULE_VIOLATED";
    public const string Unexpected = "UNEXPECTED_ERROR";
}