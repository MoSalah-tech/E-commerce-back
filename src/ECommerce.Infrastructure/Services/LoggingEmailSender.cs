using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.Services;

/// <summary>
/// Logging stand-in for outbound e-mail. Swap the interface registration for a
/// real provider (SendGrid, SES, SMTP) without touching any handler.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Welcome e-mail queued for {Email} ({FullName})", email, fullName);
        return Task.CompletedTask;
    }

    public Task SendOrderConfirmationAsync(
        string email,
        string fullName,
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Order confirmation queued for {Email} ({FullName}), order {OrderNumber}",
            email,
            fullName,
            orderNumber);

        return Task.CompletedTask;
    }
}