using ECommerce.Application.Interfaces;

namespace ECommerce.UnitTests.ApplicationTests;

/// <summary>Test double for the authenticated principal.</summary>
public sealed class FakeCurrentUserService : ICurrentUserService
{
    public bool IsAuthenticated { get; set; } = true;

    public Guid? UserId { get; set; }

    public string? Email { get; set; } = "shopper@example.com";

    public IReadOnlyCollection<string> Roles { get; set; } = [];

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static FakeCurrentUserService Anonymous() => new() { IsAuthenticated = false, UserId = null };
}

/// <summary>Test double for the clock.</summary>
public sealed class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}

/// <summary>Captures outbound e-mail instead of sending it.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public List<(string Email, string FullName)> WelcomeEmails { get; } = [];

    public List<(string Email, string FullName, string OrderNumber)> OrderConfirmations { get; } = [];

    public Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        WelcomeEmails.Add((email, fullName));
        return Task.CompletedTask;
    }

    public Task SendOrderConfirmationAsync(
        string email,
        string fullName,
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        OrderConfirmations.Add((email, fullName, orderNumber));
        return Task.CompletedTask;
    }
}
