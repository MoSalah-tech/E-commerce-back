namespace ECommerce.Application.Interfaces;

/// <summary>Identity roles recognised by the API.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All = [Admin, Customer];
}

/// <summary>Abstraction over the authenticated principal, so handlers never touch HttpContext.</summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}

/// <summary>Clock abstraction so time dependent handlers stay unit-testable.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>Outbound e-mail. Implemented as a logging stub; swap for a real provider in production.</summary>
public interface IEmailSender
{
    Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default);

    Task SendOrderConfirmationAsync(
        string email,
        string fullName,
        string orderNumber,
        CancellationToken cancellationToken = default);
}