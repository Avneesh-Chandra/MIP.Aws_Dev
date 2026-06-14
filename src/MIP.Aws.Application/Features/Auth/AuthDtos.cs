namespace MIP.Aws.Application.Features.Auth;

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive);
