using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Infrastructure.Security;

/// <summary>Issues signed HS256 access tokens from <see cref="JwtOptions"/>.</summary>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : IAccessTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Generate(AccessTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            throw new InvalidOperationException("JWT signing key is not configured.");

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, request.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new(ClaimTypes.Name, request.DisplayName),
            new(ClaimTypes.Email, request.Email)
        };

        claims.AddRange(request.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}