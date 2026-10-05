namespace ECommerce.Application.Features.Auth;

public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Token plus the identity of the principal that owns it.</summary>
public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public DateTime ExpiresAtUtc { get; set; }

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public static AuthResponse From(Interfaces.UserAccountResult account, Interfaces.AccessToken token) => new()
    {
        AccessToken = token.Token,
        TokenType = token.TokenType,
        ExpiresAtUtc = token.ExpiresAtUtc,
        UserId = account.UserId,
        Email = account.Email,
        FullName = account.FullName,
        Roles = account.Roles
    };
}