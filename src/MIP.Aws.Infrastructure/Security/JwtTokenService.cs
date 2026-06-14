using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Common;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MIP.Aws.Infrastructure.Security;

/// <summary>
/// Issues JWT access tokens and opaque refresh tokens using symmetric signing.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, ILogger<JwtTokenService> logger) : IJwtTokenService
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly JwtOptions _jwt = options.Value;

    /// <inheritdoc />
    public string CreateAccessToken(ApplicationUser user, IReadOnlyList<string> roles)
    {
        if (string.IsNullOrWhiteSpace(_jwt.SigningKey) || _jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in RolePermissionMatrix.GetPermissionsForRoles(roles))
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, permission));
        }

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-30),
            expires: expires,
            signingCredentials: credentials);

        var encoded = _tokenHandler.WriteToken(token);
        logger.LogInformation("Issued access token for user {UserId}", user.Id);
        return encoded;
    }

    /// <inheritdoc />
    public (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) CreateRefreshToken()
    {
        Span<byte> buffer = stackalloc byte[64];
        RandomNumberGenerator.Fill(buffer);
        var raw = Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = TokenHasher.Hash(raw);
        var expires = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays);
        return (raw, hash, expires);
    }

    /// <inheritdoc />
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        if (string.IsNullOrWhiteSpace(_jwt.SigningKey))
        {
            return null;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var parameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidAudience = _jwt.Audience,
            ValidIssuer = _jwt.Issuer,
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            return _tokenHandler.ValidateToken(token, parameters, out _);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to read principal from token");
            return null;
        }
    }
}
