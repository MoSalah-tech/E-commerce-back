namespace ECommerce.Application.Interfaces;

/// <summary>Input required to mint an access token.</summary>
public sealed record AccessTokenRequest(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles);

/// <summary>A signed JWT plus the metadata a client needs to refresh it.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc, string TokenType = "Bearer");

/// <summary>Issues signed access tokens. Implemented in Infrastructure with <c>System.IdentityModel</c>.</summary>
public interface IAccessTokenGenerator
{
    AccessToken Generate(AccessTokenRequest request);
}

/// <summary>
/// Identity facade used by the auth feature. Keeps ASP.NET Identity types out
/// of the Application layer entirely.
/// </summary>
public interface IUserAccountService
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<UserAccountResult> CreateCustomerAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken = default);

    Task<UserAccountResult> CreateAdminAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken = default);

    Task<UserAccountResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<UserAccountResult?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of an identity operation, already shaped for a <c>Result</c>.</summary>
public sealed record UserAccountResult(
    bool Succeeded,
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Errors)
{
    public static UserAccountResult Success(
        Guid userId,
        string email,
        string fullName,
        IReadOnlyList<string> roles) =>
        new(true, userId, email, fullName, roles, []);

    public static UserAccountResult Fail(IReadOnlyList<string> errors) =>
        new(false, Guid.Empty, string.Empty, string.Empty, [], errors);

    public static UserAccountResult Fail(string error) =>
        new(false, Guid.Empty, string.Empty, string.Empty, [], [error]);
}