using System.Security.Claims;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Abstractions.Security;

/// <summary>
/// Issues and validates JWT access tokens for local identity users.
/// </summary>
public interface IJwtTokenService
{
    string CreateAccessToken(ApplicationUser user, IReadOnlyList<string> roles);

    (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) CreateRefreshToken();

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
