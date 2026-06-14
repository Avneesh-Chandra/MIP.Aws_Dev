using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Social;

public sealed class SocialPlatformAccount : AuditableEntity
{
    public string DisplayName { get; set; } = string.Empty;

    public SocialPlatform Platform { get; set; }

    public SocialAccountType AccountType { get; set; }

    public string? Handle { get; set; }

    public string? AccountEmail { get; set; }

    public string? EnvironmentName { get; set; }

    public string? ExternalAccountId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsDefaultForPlatform { get; set; }

    public string? ProtectedTokenPayload { get; set; }

    public string? ProtectedRefreshTokenPayload { get; set; }

    public DateTimeOffset? TokenExpiresAt { get; set; }

    public DateTimeOffset? LastConnectedAt { get; set; }

    public DateTimeOffset? LastTestedAt { get; set; }

    public string? LastTestOutcome { get; set; }

    public string? ConnectionHealth { get; set; }

    public string? ConnectionStatus { get; set; }

    public string? Scopes { get; set; }

    public string? LastConnectionError { get; set; }

    public string? OAuthState { get; set; }
}
